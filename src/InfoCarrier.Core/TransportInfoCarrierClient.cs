// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfoCarrier.Core;

/// <summary>
///     An <see cref="IInfoCarrierClient" /> implemented over the <see cref="IInfoCarrierTransport" />
///     seam. Wraps each operation's payload in a versioned <see cref="InfoCarrierEnvelope" />,
///     sends it, and unwraps the response payload.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="TransportInfoCarrierClient" /> class.
/// </remarks>
public sealed class TransportInfoCarrierClient(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer)
    : IInfoCarrierClient, IInfoCarrierClientConfiguration
{
    private readonly IInfoCarrierTransport _transport = transport;
    private readonly bool _measureRequests = true;
    private readonly IInfoCarrierSerializer _serializer = serializer;
    private readonly ILogger<TransportInfoCarrierClient> _logger = NullLogger<TransportInfoCarrierClient>.Instance;

    InfoCarrierClientRegistration? IInfoCarrierClientConfiguration.Registration { get; set; }

    /// <inheritdoc />
    public async IAsyncEnumerable<QueryStreamFrame> QueryDataStreamAsync(
        QueryDataRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_transport is not IInfoCarrierStreamTransport streamTransport)
        {
            QueryDataResult buffered = await QueryDataAsync(request, clientContext, cancellationToken)
                .ConfigureAwait(false);
            yield return new QueryStartFrame(
                QueryStreamFrameCodec.Version, 0, buffered.IsEntityResult, buffered.ElementTypeName);
            List<DynamicValueNode> rows = BufferedQueryFrameAdapter.DeserializeRows(buffered.SerializedResults);
            foreach (DynamicValueNode row in rows)
            {
                yield return new QueryRowFrame(row);
            }

            foreach (ServerLogEvent log in buffered.ServerLog ?? [])
            {
                yield return new QueryLogFrame(log);
            }

            yield return new QueryCompleteFrame();

            yield break;
        }

        await foreach (QueryStreamFrame frame in ReadQueryFramesAsync(streamTransport, request, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    /// <summary>Creates a client with an explicitly supplied class logger.</summary>
    /// <param name="transport">The transport used for exchanges.</param>
    /// <param name="serializer">The payload serializer.</param>
    /// <param name="logger">The host-owned logger.</param>
    public TransportInfoCarrierClient(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer,
        ILogger<TransportInfoCarrierClient> logger) : this(transport, serializer)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    internal TransportInfoCarrierClient(
        IInfoCarrierTransport transport,
        IInfoCarrierSerializer serializer,
        ILogger<TransportInfoCarrierClient> logger,
        InfoCarrierClientRegistration registration, bool measureRequests = true)
        : this(transport, serializer, logger)
    {
        ((IInfoCarrierClientConfiguration)this).Registration = registration;
        _measureRequests = measureRequests;
    }

    /// <inheritdoc />
    public QueryDataResult QueryData(
        QueryDataRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext)
        => RoundTrip<QueryDataRequest, QueryDataResult>(InfoCarrierOperation.Query, request);

    /// <inheritdoc />
    public async Task<QueryDataResult> QueryDataAsync(
        QueryDataRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext,
        CancellationToken cancellationToken = default)
        => _transport is IInfoCarrierStreamTransport streamTransport
            ? await StreamQueryAsync(streamTransport, request, cancellationToken).ConfigureAwait(false)
            : await RoundTripAsync<QueryDataRequest, QueryDataResult>(
                InfoCarrierOperation.Query, request, cancellationToken).ConfigureAwait(false);

    private async Task<QueryDataResult> StreamQueryAsync(
        IInfoCarrierStreamTransport transport,
        QueryDataRequest request,
        CancellationToken cancellationToken)
    {
        QueryStartFrame? start = null;
        var rows = new List<DynamicValueNode>();
        var logs = new List<ServerLogEvent>();
        await foreach (QueryStreamFrame frame in ReadQueryFramesAsync(transport, request, cancellationToken)
            .ConfigureAwait(false))
        {
            switch (frame)
            {
                case QueryStartFrame value:
                    start = value;
                    break;
                case QueryRowFrame value:
                    rows.Add(value.Value);
                    break;
                case QueryLogFrame value:
                    logs.Add(value.Event);
                    break;
                case QueryFaultFrame value:
                    throw InfoCarrierFaultMapper.Rehydrate(value.Fault);
            }
        }

        return new QueryDataResult
        {
            SerializedResults = JsonSerializer.SerializeToUtf8Bytes(
                rows, ExpressionJsonContext.Default.ListDynamicValueNode),
            IsEntityResult = start?.IsEntityResult ?? false,
            ElementTypeName = start?.ElementTypeName,
            ServerLog = logs.Count == 0 ? null : logs,
        };
    }

    private async IAsyncEnumerable<QueryStreamFrame> ReadQueryFramesAsync(
        IInfoCarrierStreamTransport transport, QueryDataRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using InfoCarrierRequestMeasurement? measurement = _measureRequests ? InfoCarrierMetrics.StartClient(
            ((IInfoCarrierClientConfiguration)this).Registration?.MetricIdentity, InfoCarrierOperation.Query) : null;
        IInfoCarrierResponse? response = null;
        Stream? bounded = null;
        IAsyncEnumerator<QueryStreamFrame>? frames = null;
        try
        {
            try
            {
                response = await transport.SendAsync(InfoCarrierOperation.Query,
                    (destination, token) => _serializer.SerializeAsync(destination, request, token),
                    cancellationToken).ConfigureAwait(false);
                InfoCarrierPayloadLimits? limits = _serializer switch
                {
                    InfoCarrierSerializerBoundary boundary => boundary.Limits,
                    SystemTextJsonInfoCarrierSerializer json => json.Limits,
                    _ => null,
                };
                bounded = limits?.CreateReadLimit<QueryDataResult>(response.Content);
                frames = QueryStreamFrameCodec.ReadAsync(bounded ?? response.Content, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
            }
            catch (Exception exception)
            {
                measurement?.Fail(exception, cancellationToken);
                throw;
            }

            while (true)
            {
                bool next;
                try { next = await frames.MoveNextAsync().ConfigureAwait(false); }
                catch (Exception exception)
                {
                    measurement?.Fail(exception, cancellationToken);
                    throw;
                }
                if (!next) { yield break; }
                QueryStreamFrame frame = frames.Current;
                if (frame is QueryCompleteFrame) { measurement?.Complete(); }
                if (frame is QueryFaultFrame fault)
                {
                    measurement?.FailFault(fault.Fault);
                }
                yield return frame;
            }
        }
        finally
        {
            try
            {
                if (frames is not null) { await frames.DisposeAsync().ConfigureAwait(false); }
            }
            catch (Exception exception)
            {
                measurement?.Fail(exception, cancellationToken);
                throw;
            }
            finally
            {
                bounded?.Dispose();
                if (response is not null)
                {
                    try { await response.DisposeAsync().ConfigureAwait(false); }
                    catch (Exception exception)
                    {
                        measurement?.Fail(exception, cancellationToken);
                        throw;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public SaveChangesResult SaveChanges(
        SaveChangesRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext)
        => RoundTrip<SaveChangesRequest, SaveChangesResult>(InfoCarrierOperation.SaveChanges, request);

    /// <inheritdoc />
    public async Task<SaveChangesResult> SaveChangesAsync(
        SaveChangesRequest request,
        Microsoft.EntityFrameworkCore.DbContext clientContext,
        CancellationToken cancellationToken = default)
        => await RoundTripAsync<SaveChangesRequest, SaveChangesResult>(
            InfoCarrierOperation.SaveChanges, request, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public TransactionResult BeginTransaction()
        => RoundTrip<object?, TransactionResult>(InfoCarrierOperation.BeginTransaction, null);

    /// <inheritdoc />
    public async Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
        // `null`, not `new { }`. BeginTransaction takes no arguments, so the payload is a
        // placeholder either way and the server never reads it -- but an anonymous type declared
        // as `object` is serialized by its RUNTIME type, which a source-generated context cannot
        // have metadata for. It failed outright once the resolver was set:
        // "JsonTypeInfo metadata for type '<>f__AnonymousType0' was not provided". `null` is what
        // every other void operation already puts there.
        => await RoundTripAsync<object?, TransactionResult>(
            InfoCarrierOperation.BeginTransaction, null, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void CommitTransaction(string transactionId)
        => RoundTrip<string, object>(InfoCarrierOperation.CommitTransaction, transactionId);

    /// <inheritdoc />
    public async Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, object>(
            InfoCarrierOperation.CommitTransaction, transactionId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void RollbackTransaction(string transactionId)
        => RoundTrip<string, object>(InfoCarrierOperation.RollbackTransaction, transactionId);

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, object>(
            InfoCarrierOperation.RollbackTransaction, transactionId, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void CreateSavepoint(string transactionId, string name)
        => RoundTrip<SavepointRequest, object>(
            InfoCarrierOperation.CreateSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name });

    /// <inheritdoc />
    public async Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.CreateSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void RollbackToSavepoint(string transactionId, string name)
        => RoundTrip<SavepointRequest, object>(
            InfoCarrierOperation.RollbackToSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name });

    /// <inheritdoc />
    public async Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.RollbackToSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public void ReleaseSavepoint(string transactionId, string name)
        => RoundTrip<SavepointRequest, object>(
            InfoCarrierOperation.ReleaseSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name });

    /// <inheritdoc />
    public async Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
        => await RoundTripAsync<SavepointRequest, object>(
            InfoCarrierOperation.ReleaseSavepoint,
            new SavepointRequest { TransactionId = transactionId, Name = name },
            cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public bool SupportsSavepoints(string transactionId)
        => RoundTrip<string, bool>(InfoCarrierOperation.SupportsSavepoints, transactionId);

    /// <inheritdoc />
    public async Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
        => await RoundTripAsync<string, bool>(
            InfoCarrierOperation.SupportsSavepoints, transactionId, cancellationToken).ConfigureAwait(false);

    private TResponse RoundTrip<TRequest, TResponse>(InfoCarrierOperation operation, TRequest request)
    {
        using InfoCarrierRequestMeasurement? measurement = _measureRequests ? InfoCarrierMetrics.StartClient(
            ((IInfoCarrierClientConfiguration)this).Registration?.MetricIdentity, operation) : null;

        try
        {
            TResponse response = Send<TRequest, TResponse>(operation, request);
            measurement?.Complete();
            return response;
        }
        catch (Exception exception)
        {
            measurement?.Fail(exception, default);
            throw;
        }
    }

    private async Task<TResponse> RoundTripAsync<TRequest, TResponse>(
        InfoCarrierOperation operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        // Buffered operations share this boundary. Since 2026-10-25 streamed queries use their
        // response-enumeration boundary, so their duration includes reading and response disposal.
        // Starting without listeners allocates no measurement and takes no timestamp.
        using InfoCarrierRequestMeasurement? measurement = _measureRequests ? InfoCarrierMetrics.StartClient(
            ((IInfoCarrierClientConfiguration)this).Registration?.MetricIdentity, operation) : null;

        try
        {
            TResponse response = await SendAsync<TRequest, TResponse>(operation, request, cancellationToken)
                .ConfigureAwait(false);
            measurement?.Complete();
            return response;
        }
        catch (Exception exception)
        {
            // A failed round trip cost a round trip. Counting only the successes would understate
            // exactly the case a reader is investigating.
            measurement?.Fail(exception, cancellationToken);
            throw;
        }
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        InfoCarrierOperation operation,
        TRequest request,
        CancellationToken cancellationToken)
    {
        string phase = "request payload serialization";
        bool returnedFault = false;
        try
        {
            var envelope = new InfoCarrierEnvelope
            {
                ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
                Operation = operation,
                Payload = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false),
            };

            phase = "transport exchange";
            InfoCarrierEnvelope response = await _transport.SendAsync(envelope, cancellationToken).ConfigureAwait(false);

            // The fault first, and before the payload is looked at (wire-protocol W5). A failed
            // response carries a placeholder payload, so reading it first would deserialize a null
            // and report the failure as an empty result — which is the one way an error can do more
            // damage than the operation that caused it.
            if (response.Fault is { } fault)
            {
                phase = "returned server fault";
                returnedFault = true;
                throw InfoCarrierFaultMapper.Rehydrate(fault);
            }

            phase = "response payload deserialization";
            return (await _serializer.DeserializeAsync<TResponse>(response.Payload, cancellationToken).ConfigureAwait(false))!;
        }
        catch (Exception exception)
        {
            LogFailure(operation, phase, exception, cancellationToken, returnedFault);
            throw;
        }
    }

    private TResponse Send<TRequest, TResponse>(InfoCarrierOperation operation, TRequest request)
    {
        string phase = "request payload serialization";
        bool returnedFault = false;
        try
        {
            var envelope = new InfoCarrierEnvelope
            {
                ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
                Operation = operation,
                Payload = _serializer.Serialize(request),
            };

            phase = "transport exchange";
            InfoCarrierEnvelope response = _transport.Send(envelope);

            if (response.Fault is { } fault)
            {
                phase = "returned server fault";
                returnedFault = true;
                throw InfoCarrierFaultMapper.Rehydrate(fault);
            }

            phase = "response payload deserialization";
            return _serializer.Deserialize<TResponse>(response.Payload)!;
        }
        catch (Exception exception)
        {
            LogFailure(operation, phase, exception, default, returnedFault);
            throw;
        }
    }

    private void LogFailure(InfoCarrierOperation operation, string phase, Exception exception,
        CancellationToken cancellationToken, bool returnedFault)
    {
        (LogLevel level, string outcome) = exception switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested => (LogLevel.Debug, "CallerCancellation"),
            OperationCanceledException => (LogLevel.Error, "UnrelatedCancellation"),
            DbUpdateConcurrencyException => (LogLevel.Information, "ConcurrencyConflict"),
            _ when returnedFault => (LogLevel.Information, "ServerFaultReceived"),
            _ => (LogLevel.Error, "UnclassifiedFailure"),
        };
        try
        {
            if (_logger.IsEnabled(level))
            {
                _logger.Log(level, new EventId(35100),
                    "InfoCarrier client {Operation} failed during {Phase}. Outcome={Outcome}; ExceptionType={ExceptionType}.",
                    operation, phase, outcome, exception.GetType().FullName);
            }
        }
        catch (Exception)
        {
            // Event delivery must not replace the operation failure.
        }
    }
}
