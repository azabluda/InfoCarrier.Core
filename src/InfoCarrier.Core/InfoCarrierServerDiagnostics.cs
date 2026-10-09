// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Safe, bounded server failure diagnostics. Share one instance per host.
/// </summary>
/// <remarks>
///     Uses the InfoCarrier.Server logger category and InfoCarrier.Server meter. Each reason
///     emits at most five detailed events per minute; the next event after that interval also
///     reports the suppressed count. Counters count every failure, including suppressed events.
///     Neither exception objects nor their messages, payloads, SQL, or transaction tokens are logged.
///     Caller logging is opt-in and uses a host-keyed hash of the trusted host identity.
/// </remarks>
/// <param name="meterFactory">The host meter factory, which owns the meter lifetime.</param>
/// <param name="timeProvider">The monotonic clock used for suppression windows.</param>
/// <param name="includeCallerIdentity">Include a pseudonymous trusted caller identifier.</param>
public sealed class InfoCarrierServerDiagnostics(
    IMeterFactory meterFactory,
    TimeProvider timeProvider,
    bool includeCallerIdentity = false)
{
    private readonly AsyncLocal<Request?> _current = new();
    private readonly byte[] _identityKey = RandomNumberGenerator.GetBytes(32);
    private readonly Counter<long> _failures = meterFactory.Create(new MeterOptions("InfoCarrier.Server"))
        .CreateCounter<long>("infocarrier.server.failures");
    private readonly Counter<long> _suppressed = meterFactory.Create(new MeterOptions("InfoCarrier.Server"))
        .CreateCounter<long>("infocarrier.server.suppressed");
    private readonly Window[] _windows = Enumerable.Range(0, Enum.GetValues<InfoCarrierServerFailureReason>().Length)
        .Select(_ => new Window()).ToArray();
    private readonly TimeProvider _clock = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly bool _includeCaller = includeCallerIdentity;

    /// <summary>Begins a request diagnostic scope without adding wire fields.</summary>
    /// <param name="operation">The operation, or null before envelope parsing.</param>
    /// <param name="loggerFactory">The factory from the current request scope.</param>
    /// <param name="callerIdentity">A trusted host identity, used only when explicitly enabled.</param>
    /// <returns>The scope to dispose after the response is written.</returns>
    public IDisposable BeginRequest(Common.InfoCarrierOperation? operation, ILoggerFactory? loggerFactory = null, string? callerIdentity = null)
    {
        ILogger? logger = Logger(loggerFactory);
        Request? outer = _current.Value;
        if (outer?.Owner == this)
        {
            outer.Operation = OperationName(operation);
            return new Restore(_current, outer);
        }

        _current.Value = new Request(this, logger, OperationName(operation),
            Guid.NewGuid().ToString("N"),
            _includeCaller ? CallerHash(callerIdentity) : null);
        return new Restore(_current, outer);
    }

    /// <summary>Identifies the current server phase using a bounded value.</summary>
    /// <param name="phase">The phase entered by trusted server code.</param>
    public void SetPhase(InfoCarrierServerPhase phase)
    {
        if (_current.Value is { } request)
        {
            request.Phase = Enum.IsDefined(phase) ? phase.ToString() : "Unknown";
        }
    }

    internal string CurrentPhase => _current.Value?.Phase ?? "Execution";

    /// <summary>Records one safe failure outcome for the current request.</summary>
    /// <param name="exception">The failure; its message, stack and data are never logged.</param>
    /// <param name="token">The request token, used to distinguish expected cancellation.</param>
    /// <param name="reason">An optional fixed reason supplied by trusted host code.</param>
    /// <param name="phase">An optional fixed phase overriding the current phase.</param>
    public void ReportFailure(Exception exception, CancellationToken token = default,
        InfoCarrierServerFailureReason? reason = null, InfoCarrierServerPhase? phase = null)
    {
        if (_current.Value is not { } request || request.Reported)
        {
            return;
        }

        request.Reported = true;
        try
        {
            string failurePhase = phase is { } value && Enum.IsDefined(value) ? value.ToString() : request.Phase;
            InfoCarrierServerFailureReason failureReason = reason is { } selected && Enum.IsDefined(selected)
                ? selected : ServerFailureClassification.Reason(exception, token, failurePhase);
            Emit(request.Logger, failureReason, failurePhase, request.Operation, request.Id, request.Caller);
        }
        catch (Exception) { }
    }

    internal void Cleanup(ILogger? logger, InfoCarrierServerFailureReason reason, string phase, string id)
        => Emit(logger, reason, phase, "Cleanup", id, null);

    internal static ILogger? Logger(IServiceProvider provider)
    {
        try
        {
            return Logger(provider.GetService<ILoggerFactory>());
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static ILogger? Logger(ILoggerFactory? factory)
    {
        try
        {
            return factory?.CreateLogger("InfoCarrier.Server");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void Emit(ILogger? logger, InfoCarrierServerFailureReason reason, string phase, string operation, string id, string? caller)
    {
        try { EmitCore(logger, reason, phase, operation, id, caller); }
        catch (Exception) { }
    }

    private void EmitCore(ILogger? logger, InfoCarrierServerFailureReason reason, string phase, string operation, string id, string? caller)
    {
        // Listener and logger failures must never replace the operation's exception or stop cleanup.
        try
        {
            if (reason is not InfoCarrierServerFailureReason.CleanupCompleted and not InfoCarrierServerFailureReason.TransactionEvicted)
            {
                _failures.Add(1, new KeyValuePair<string, object?>("reason", reason.ToString()));
            }
        }
        catch (Exception) { }

        bool emit;
        long summary = 0;
        Window window = _windows[(int)reason];
        lock (window)
        {
            long now = _clock.GetTimestamp();
            if (!window.Started || _clock.GetElapsedTime(window.Start, now) >= TimeSpan.FromMinutes(1))
            {
                summary = window.Suppressed;
                window.Started = true;
                window.Start = now;
                window.Emitted = 0;
                window.Suppressed = 0;
            }

            emit = window.Emitted < 5;
            if (emit) { window.Emitted++; }
            else { window.Suppressed++; }
        }

        if (!emit)
        {
            try
            {
                _suppressed.Add(1, new KeyValuePair<string, object?>("reason", reason.ToString()));
            }
            catch (Exception) { }
        }

        try
        {
            LogLevel level = ServerFailureClassification.Level(reason);
            if (summary > 0)
            {
                logger?.Log(level, new EventId(35003, "FailuresSuppressed"),
                    "InfoCarrier suppressed {SuppressedCount} server events for {Reason} in the previous window.",
                    summary, reason.ToString());
            }

            if (emit)
            {
                logger?.Log(level, new EventId(operation == "Cleanup" ? 35002 : 35001,
                    operation == "Cleanup" ? "CleanupOutcome" : "RequestFailed"),
                    "InfoCarrier server outcome {Reason} in {Phase}; operation {Operation}, request {RequestId}, caller {CallerId}.",
                    reason.ToString(), phase, operation, id, caller);
            }
        }
        catch (Exception) { }
    }

    private string? CallerHash(string? caller)
        => string.IsNullOrEmpty(caller) || caller.Length > 1024 ? null
            : Convert.ToHexString(HMACSHA256.HashData(_identityKey, Encoding.UTF8.GetBytes(caller)));

    private static string OperationName(Common.InfoCarrierOperation? operation)
        => operation is { } value && Enum.IsDefined(value) ? value.ToString() : "Unknown";

    private sealed class Window
    {
        public bool Started;
        public long Start;
        public int Emitted;
        public long Suppressed;
    }

    private sealed class Request(InfoCarrierServerDiagnostics owner, ILogger? logger, string operation, string id, string? caller)
    {
        public InfoCarrierServerDiagnostics Owner { get; } = owner;
        public ILogger? Logger { get; } = logger;
        public string Operation { get; set; } = operation;
        public string Id { get; } = id;
        public string? Caller { get; } = caller;
        public string Phase { get; set; } = "Input";
        public bool Reported { get; set; }
    }

    private sealed class Restore(AsyncLocal<Request?> current, Request? outer) : IDisposable
    {
        public void Dispose() => current.Value = outer;
    }
}

internal static class ServerFailureClassification
{
    internal static T Mark<T>(T exception, InfoCarrierServerFailureReason reason) where T : Exception
    {
        // The key is a CLR type, never a client-supplied string. Fault mapping ignores it.
        try { exception.Data[typeof(InfoCarrierServerFailureReason)] = reason; }
        catch (Exception) { }
        return exception;
    }

    internal static InfoCarrierServerFailureReason Reason(Exception exception, CancellationToken token, string phase)
    {
        if (exception is IOException && token.IsCancellationRequested && phase is "Input" or "ResponseWrite")
        {
            return InfoCarrierServerFailureReason.Cancellation;
        }

        if (exception is OperationCanceledException)
        {
            return token.IsCancellationRequested ? InfoCarrierServerFailureReason.Cancellation : InfoCarrierServerFailureReason.UnexpectedCancellation;
        }

        if (phase is "ResultSerialization" or "FaultSerialization" or "EnvelopeSerialization")
        {
            return InfoCarrierServerFailureReason.ResponseSerialization;
        }

        try
        {
            if (exception.Data[typeof(InfoCarrierServerFailureReason)] is InfoCarrierServerFailureReason reason && Enum.IsDefined(reason))
            {
                return reason;
            }
        }
        catch (Exception) { }

        return exception switch
        {
            DbUpdateConcurrencyException => InfoCarrierServerFailureReason.Concurrency,
            DbUpdateException or System.Data.Common.DbException => InfoCarrierServerFailureReason.Database,
            System.Text.Json.JsonException when phase is "Input" or "Validation" => InfoCarrierServerFailureReason.InvalidPayload,
            _ when phase == "Rebinding" => InfoCarrierServerFailureReason.RebindingFailure,
            _ when phase == "ResultMapping" => InfoCarrierServerFailureReason.ResultMappingFailure,
            _ when phase == "ResponseWrite" => InfoCarrierServerFailureReason.ResponseWrite,
            _ => InfoCarrierServerFailureReason.ExecutionFailure,
        };
    }

    internal static LogLevel Level(InfoCarrierServerFailureReason reason) => reason switch
    {
        InfoCarrierServerFailureReason.InvalidPayload or InfoCarrierServerFailureReason.PayloadLimit or InfoCarrierServerFailureReason.InvalidDescriptor
            or InfoCarrierServerFailureReason.Permission or InfoCarrierServerFailureReason.UnknownOperation or InfoCarrierServerFailureReason.Concurrency
            or InfoCarrierServerFailureReason.Cancellation or InfoCarrierServerFailureReason.CleanupCompleted => LogLevel.Information,
        InfoCarrierServerFailureReason.Configuration or InfoCarrierServerFailureReason.ProtocolVersion or InfoCarrierServerFailureReason.TransactionNotOpen
            or InfoCarrierServerFailureReason.WrongInstance or InfoCarrierServerFailureReason.CallerMismatch or InfoCarrierServerFailureReason.TransactionEvicted => LogLevel.Warning,
        _ => LogLevel.Error,
    };
}
