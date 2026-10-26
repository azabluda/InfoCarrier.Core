// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Linq.Expressions;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using InfoCarrier.Core.ValueMapping;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Context = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Context;
using Item = InfoCarrier.Core.TransportTests.ClientServerCoexistenceTest.Item;

namespace InfoCarrier.Core.TransportTests;

public partial class ServiceReplacementTest
{
    [Fact]
    public async Task Selected_server_limits_also_guard_decoded_query_payloads_for_custom_adapters()
    {
        byte[] query;
        await using (var modelHost = await Host.Start("name", custom: false))
        {
            using IServiceScope scope = modelHost.App.Services.CreateScope();
            Context context = scope.ServiceProvider.GetRequiredService<Context>();
            Expression count = Expression.Call(typeof(Queryable), nameof(Queryable.Count), [typeof(Item)],
                ((IQueryable<Item>)context.Items).Expression);
            ExpressionNode node = ExpressionSerializer.CreateForModel(context.Model).ToNode(count);
            query = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(node, ExpressionJsonContext.Default.ExpressionNode);
        }
        var request = new QueryDataRequest
        {
            SerializedQuery = query, TrackingBehavior = QueryTrackingBehavior.NoTracking,
            IsAsync = true, ReturnsSingleResult = true,
        };
        await using var limited = await Host.Start("name", custom: false,
            configure: options => options.MaxRequestBytes = query.Length - 1);
        await using var unlimited = await Host.Start("name", custom: false,
            configure: options => options.MaxRequestBytes = null);
        IInfoCarrierServer limitedServer = limited.App.Services.GetInfoCarrierServerEndpoint("orders").Server;
        IInfoCarrierServer unlimitedServer = unlimited.App.Services.GetInfoCarrierServerEndpoint("orders").Server;
        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(() => limitedServer.QueryDataAsync(request));
        Assert.Contains("MaxRequestBytes", failure.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (QueryStreamFrame _ in ((IInfoCarrierStreamServer)limitedServer).QueryFramesAsync(request)) { }
        });
        QueryDataResult result = await unlimitedServer.QueryDataAsync(request);
        DynamicValueNode row = Assert.Single(BufferedQueryFrameAdapter.DeserializeRows(result.SerializedResults));
        Assert.Equal("0", row.PrimitiveValue?.ToString());
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("async")]
    [InlineData("stream")]
    public async Task Failed_context_resolution_disposes_the_operation_scope(string mode)
    {
        int disposed = 0;
        var services = new ServiceCollection().AddLogging();
        services.AddScoped(_ => new ResolutionProbe(() => Interlocked.Increment(ref disposed)));
        services.AddScoped(_ => new AsyncResolutionProbe(() => Interlocked.Increment(ref disposed)));
        services.AddInfoCarrierServer<Context>("failing", configure: null, resolveContext: provider =>
        {
            if (mode == "sync") { _ = provider.GetRequiredService<ResolutionProbe>(); }
            else { _ = provider.GetRequiredService<AsyncResolutionProbe>(); }
            throw new InvalidOperationException("Controlled context construction failure.");
        });
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        IInfoCarrierServer server = provider.GetInfoCarrierServerEndpoint("failing").Server;
        var request = new QueryDataRequest
        {
            SerializedQuery = [], TrackingBehavior = QueryTrackingBehavior.TrackAll,
            IsAsync = false, ReturnsSingleResult = false,
        };
        if (mode == "sync") { Assert.Throws<InvalidOperationException>(() => server.QueryData(request)); }
        else if (mode == "async") { await Assert.ThrowsAsync<InvalidOperationException>(() => server.QueryDataAsync(request)); }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (QueryStreamFrame _ in ((IInfoCarrierStreamServer)server).QueryFramesAsync(request)) { }
            });
        }
        Assert.Equal(1, disposed);
    }

    private sealed class AsyncResolutionProbe(Action dispose) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
    }

    private sealed class ResolutionProbe(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Custom_pipeline_lives_until_stream_disposal_or_cancellation(bool cancel)
    {
        var calls = new PipelineCalls();
        await using var host = await Host.Start("name", custom: false, server =>
        {
            server.Services.AddSingleton(calls);
            server.ReplaceService<IInfoCarrierServerPipelineFactory, RecordingPipelineFactory>();
        });
        QueryDataRequest request;
        using (IServiceScope scope = host.App.Services.CreateScope())
        {
            Context context = scope.ServiceProvider.GetRequiredService<Context>();
            context.Add(new Item { Id = 1, Name = "streamed" });
            await context.SaveChangesAsync();
            ExpressionNode node = ExpressionSerializer.CreateForModel(context.Model).ToNode(
                ((IQueryable<Item>)context.Items).Expression);
            request = new QueryDataRequest
            {
                SerializedQuery = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(node, ExpressionJsonContext.Default.ExpressionNode),
                TrackingBehavior = QueryTrackingBehavior.TrackAll, IsAsync = true, ReturnsSingleResult = false,
            };
        }
        var server = Assert.IsAssignableFrom<IInfoCarrierStreamServer>(host.App.Services.GetInfoCarrierServerEndpoint("orders").Server);
        using var cancellation = new CancellationTokenSource();
        await using (IAsyncEnumerator<QueryStreamFrame> frames = server.QueryFramesAsync(request, cancellation.Token)
            .GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await frames.MoveNextAsync());
            Assert.Equal(1, calls.Pipelines);
            Assert.Equal(0, calls.PipelineDisposals);
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                {
                    while (await frames.MoveNextAsync()) { }
                });
            }
        }
        Assert.Equal(1, calls.PipelineDisposals);
    }

    [Fact]
    public async Task Custom_pipeline_is_disposed_after_sync_async_and_streaming_translation_failures()
    {
        var calls = new PipelineCalls();
        await using var host = await Host.Start("name", custom: false, server =>
        {
            server.Services.AddSingleton(calls);
            server.ReplaceService<IInfoCarrierServerPipelineFactory, RecordingPipelineFactory>();
        });
        IInfoCarrierServer server = host.App.Services.GetInfoCarrierServerEndpoint("orders").Server;
        var request = new QueryDataRequest
        {
            SerializedQuery = "{"u8.ToArray(), TrackingBehavior = QueryTrackingBehavior.TrackAll,
            IsAsync = false, ReturnsSingleResult = false,
        };
        Assert.ThrowsAny<Exception>(() => server.QueryData(request));
        await Assert.ThrowsAnyAsync<Exception>(() => server.QueryDataAsync(request));
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (QueryStreamFrame _ in Assert.IsAssignableFrom<IInfoCarrierStreamServer>(server)
                .QueryFramesAsync(request)) { }
        });
        Assert.Equal(3, calls.Pipelines);
        Assert.Equal(3, calls.PipelineDisposals);
    }

    [Fact]
    public async Task HTTP_media_type_applies_to_custom_payloads_while_stream_frames_remain_JSON()
    {
        const string mediaType = "application/vnd.orders";
        await using var host = await Host.Start("name", custom: true, configure: options => options.MediaType = mediaType);
        var exchanges = new ConcurrentBag<(string? Request, string? Response)>();
        var services = new ServiceCollection();
        services.AddSingleton<Observations>();
        services.AddInfoCarrierClient("orders", options =>
        {
            options.Endpoint = new Uri("http://localhost/infocarrier");
            options.MediaType = mediaType;
        }).ReplaceService<IInfoCarrierSerializer, PrefixSerializer>()
            .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler())
            .AddHttpMessageHandler(() => new MediaHandler(exchanges));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await Write(provider, "orders", "media");
        await host.Verify("media-updated");
        Assert.Contains((mediaType, mediaType), exchanges);
        Assert.Contains((mediaType, "application/json"), exchanges);
        Assert.All(exchanges, exchange => Assert.Equal(mediaType, exchange.Request));
    }

    private sealed class MediaHandler(ConcurrentBag<(string? Request, string? Response)> exchanges) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            exchanges.Add((request.Content?.Headers.ContentType?.MediaType, response.Content.Headers.ContentType?.MediaType));
            return response;
        }
    }

    [Theory]
    [InlineData("default")]
    [InlineData("name")]
    [InlineData("handle")]
    [InlineData("type")]
    public async Task Interface_only_pipeline_and_role_replacements_keep_tracking_saves_and_transaction_context(string selection)
    {
        var calls = new PipelineCalls();
        var host = await Host.Start(selection, custom: false, server =>
        {
            server.Services.AddSingleton(calls);
            server.ReplaceService<IInfoCarrierServerPipelineFactory, RecordingPipelineFactory>()
                .ReplaceService<IInfoCarrierServer, RecordingServer>();
        });
        await using (host)
        {
            var services = new ServiceCollection();
            services.AddSingleton(calls);
            services.AddInfoCarrierClient("orders", o => o.WithEndpoint("http://localhost/infocarrier"))
                .ConfigurePrimaryHttpMessageHandler(() => host.App.GetTestServer().CreateHandler())
                .ReplaceService<IInfoCarrierClient, RecordingClient>();
            await using (ServiceProvider provider = services.BuildServiceProvider(validateScopes: true))
            {
                DbContextOptions<Context> options = new DbContextOptionsBuilder<Context>()
                    .UseApplicationServiceProvider(provider).UseInfoCarrier("orders")
                    .ReplaceService<IExpressionSerializer, RecordingExpressionSerializer>()
                    .ReplaceService<IDynamicValueMapper, RecordingValueMapper>().Options;
                await using (var db = new Context(options))
                {
                    await using var transaction = await db.Database.BeginTransactionAsync();
                    db.Add(new Item { Id = 1, Name = "inserted" });
                    Assert.Equal(1, await db.SaveChangesAsync());
                    db.ChangeTracker.Clear();
                    Item item = await db.Items.SingleAsync();
                    Assert.Equal("inserted", item.Name);
                    Assert.Equal(EntityState.Unchanged, db.Entry(item).State);
                    Assert.Same(item, await db.Items.SingleAsync());
                    item.Name = "committed";
                    Assert.Equal(1, await db.SaveChangesAsync());
                    await transaction.CommitAsync();
                    Assert.True(Assert.IsType<RecordingValueMapper>(db.GetService<IDynamicValueMapper>()).Calls > 0);
                }
                // All server pipelines above received the same actual transaction context.
                Assert.Single(calls.Contexts.Distinct(ReferenceEqualityComparer.Instance));
                await host.Verify("committed");
                Assert.True(calls.ClientRequests > 0);
                Assert.True(calls.ServerStreams > 0);
                Assert.True(calls.ServerMapping > 0);
                Assert.Equal(calls.Pipelines, calls.PipelineDisposals);
                Assert.Equal(0, calls.ClientDisposals);
                Assert.Equal(0, calls.ServerDisposals);
            }
            Assert.Equal(1, calls.ClientDisposals);
        }
        Assert.Equal(1, calls.ServerDisposals);
        Assert.Equal(calls.Pipelines, calls.PipelineDisposals);
    }

    [Fact]
    public async Task Endpoint_free_transport_receives_immutable_named_settings_and_selected_server_pair()
    {
        await using var host = await Host.Start("name", custom: true);
        InfoCarrierServerEndpoint endpoint = host.App.Services.GetInfoCarrierServerEndpoint("orders");
        Assert.Equal(InfoCarrierPayloadLimits.DefaultMaxRequestBytes, endpoint.MaxRequestBytes);
        var services = new ServiceCollection();
        services.AddSingleton(endpoint);
        services.AddSingleton<Observations>();
        InfoCarrierClientOptions? mutable = null;
        services.AddInfoCarrierClient("orders", options =>
        {
            mutable = options;
            options.MaxResponseBytes = 100_000;
            options.MediaType = "application/vnd.orders";
        }).ReplaceService<IInfoCarrierSerializer, PrefixSerializer>()
            .ReplaceService<IInfoCarrierTransport, DirectTransport>()
            .ConfigurePrimaryHttpMessageHandler(() => throw new InvalidOperationException("Unused HTTP adapter."));
        mutable!.MaxResponseBytes = 1;
        mutable.MediaType = "application/changed";
        await using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        await Write(provider, "orders", "direct");
        await host.Verify("direct-updated");
        Assert.Same(provider.GetInfoCarrierClient("orders"), provider.GetInfoCarrierClient("orders"));
        Assert.Throws<InvalidOperationException>(() => provider.GetInfoCarrierClient("missing"));
        Assert.Throws<InvalidOperationException>(() => provider.GetInfoCarrierClient());
        Assert.Throws<InvalidOperationException>(() => host.App.Services.GetInfoCarrierServerEndpoint("missing"));
    }

    [Fact]
    public void Default_HTTP_transport_without_endpoint_fails_at_activation()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierClient(_ => { });
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains("HTTP transport requires", Assert.Throws<InvalidOperationException>(() =>
            provider.GetInfoCarrierClient()).Message);
    }

    public sealed class PipelineCalls
    {
        public ConcurrentBag<DbContext> Contexts { get; } = [];
        public int Pipelines;
        public int PipelineDisposals;
        public int ClientRequests;
        public int ServerStreams;
        public int ServerMapping;
        public int ClientDisposals;
        public int ServerDisposals;
    }

    public sealed class RecordingPipelineFactory(PipelineCalls calls) : IInfoCarrierServerPipelineFactory
    {
        public IInfoCarrierServerPipeline Create(DbContext context, IReadOnlyList<Type> allowedTypes,
            IEnumerable<IInfoCarrierValueMapper> valueMappers, AnonymousShapeCatalog anonymousShapes)
        {
            calls.Contexts.Add(context);
            Interlocked.Increment(ref calls.Pipelines);
            var resolver = new TypeNodeResolver(context.Model, TypeAllowlist.ForModel(context.Model, allowedTypes));
            resolver.UseAnonymousShapeCatalog(anonymousShapes);
            var mapper = new RecordingValueMapper(context.Model, new TypeNodeMapper(context.Model), resolver, valueMappers,
                () => Interlocked.Increment(ref calls.ServerMapping));
            return new RecordingPipeline(new RecordingExpressionSerializer(
                new ExpressionToNodeTranslator(mapper.TypeMapper, mapper), resolver, mapper), calls);
        }
    }

    private sealed class RecordingPipeline(IExpressionSerializer serializer, PipelineCalls calls) : IInfoCarrierServerPipeline
    {
        private int _disposed;
        public IExpressionSerializer Serializer { get; } = serializer;
        public void Dispose()
        {
            Assert.Equal(0, Interlocked.Exchange(ref _disposed, 1));
            Interlocked.Increment(ref calls.PipelineDisposals);
        }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

    public sealed class RecordingExpressionSerializer(ExpressionToNodeTranslator forward, TypeNodeResolver resolver,
        IDynamicValueMapper mapper) : IExpressionSerializer
    {
        private readonly ExpressionSerializer _inner = new(forward, resolver, mapper);
        public IDynamicValueMapper ValueMapper => _inner.ValueMapper;
        public void UseExecutionAllowedTypes(IReadOnlyList<Type> types) => _inner.UseExecutionAllowedTypes(types);
        public void UseServerContext(DbContext context) => _inner.UseServerContext(context);
        public void UseAnonymousShapeCatalog(AnonymousShapeCatalog catalog) => _inner.UseAnonymousShapeCatalog(catalog);
        public ExpressionNode ToNode(Expression expression) => _inner.ToNode(expression);
        public ExpressionNode ToNode(Expression expression, Metadata.IInfoCarrierRelationalQueryRoots? roots) => _inner.ToNode(expression, roots);
        public Expression ToExpression(ExpressionNode node) => _inner.ToExpression(node);
        public Expression ToExpression(ExpressionNode node, Func<QueryRootStubNode, Type, Expression> roots) => _inner.ToExpression(node, roots);
    }

    public sealed class RecordingValueMapper : IDynamicValueMapper
    {
        private readonly DynamicValueMapper _inner;
        private readonly Action _record;
        internal int Calls { get; private set; }
        public RecordingValueMapper(IModel model, TypeNodeMapper mapper, TypeNodeResolver resolver,
            IEnumerable<IInfoCarrierValueMapper> mappers)
            : this(model, mapper, resolver, mappers, () => { }) { }
        internal RecordingValueMapper(IModel model, TypeNodeMapper mapper, TypeNodeResolver resolver,
            IEnumerable<IInfoCarrierValueMapper> mappers, Action record)
        {
            _inner = new DynamicValueMapper(model, mapper, resolver, mappers);
            _record = record;
        }
        public Func<DynamicValueNode, object?>? EntityMaterializer { get => _inner.EntityMaterializer; set => _inner.EntityMaterializer = value; }
        public TypeNodeMapper TypeMapper => _inner.TypeMapper;
        public void ResetReferenceScope() { Calls++; _record(); _inner.ResetReferenceScope(); }
        public DynamicValueNode ToDynamicValue(object? value, Type type) { Calls++; _record(); return _inner.ToDynamicValue(value, type); }
        public object? FromDynamicValue(DynamicValueNode node) { Calls++; _record(); return _inner.FromDynamicValue(node); }
        public object? FromPropertyValue(DynamicPropertyValue property, Type type) => _inner.FromPropertyValue(property, type);
        public void RegisterMaterialized(int id, object? value) => _inner.RegisterMaterialized(id, value);
        public object? FromShape(DynamicValueNode node) => _inner.FromShape(node);
        public DynamicValueNode ToComplexValue(object? value, IComplexProperty property) => _inner.ToComplexValue(value, property);
        public DynamicValueNode ToRowValue(object? value, Type type, Func<object, INavigationBase, bool> loaded,
            Func<object, bool> tracked, Func<object, IProperty, object?> shadow,
            Func<object, ISkipNavigation, bool, IEnumerable<object>> joins, Func<object, IEntityType?> entity)
            => _inner.ToRowValue(value, type, loaded, tracked, shadow, joins, entity);
        public DynamicValueNode ToRowValue(object? value, Type type, Func<object, INavigationBase, bool> loaded,
            Func<object, bool> tracked, Func<object, IProperty, object?> shadow,
            Func<object, ISkipNavigation, bool, IEnumerable<object>> joins, Func<object, IEntityType?> entity, ProjectionShape? shape)
            => _inner.ToRowValue(value, type, loaded, tracked, shadow, joins, entity, shape);
    }

    public sealed class DirectTransport : IInfoCarrierTransport
    {
        private readonly InfoCarrierEnvelopeServer _server;
        public DirectTransport([FromKeyedServices] InfoCarrierClientSettings settings, InfoCarrierServerEndpoint endpoint)
        {
            Assert.Null(settings.Endpoint);
            Assert.Equal("orders", settings.Name);
            Assert.Equal(100_000, settings.MaxResponseBytes);
            Assert.Equal("application/vnd.orders", settings.MediaType);
            _server = new InfoCarrierEnvelopeServer(endpoint.Server, endpoint.Serializer);
        }
        public InfoCarrierEnvelope Send(InfoCarrierEnvelope request) => _server.Dispatch(request);
        public Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => _server.DispatchAsync(request, cancellationToken);
    }

    public sealed class RecordingServer([FromKeyedServices] InProcessInfoCarrierServer inner, PipelineCalls calls)
        : IInfoCarrierServer, IInfoCarrierStreamServer, IDisposable
    {
        public void Dispose() => Interlocked.Increment(ref calls.ServerDisposals);
        public IAsyncEnumerable<QueryStreamFrame> QueryFramesAsync(QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls.ServerStreams);
            return inner.QueryFramesAsync(request, cancellationToken);
        }
        public QueryDataResult QueryData(QueryDataRequest request) => inner.QueryData(request);
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, CancellationToken cancellationToken = default) => inner.QueryDataAsync(request, cancellationToken);
        public SaveChangesResult SaveChanges(SaveChangesRequest request) => inner.SaveChanges(request);
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken = default) => inner.SaveChangesAsync(request, cancellationToken);
        public TransactionResult BeginTransaction() => inner.BeginTransaction();
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default) => inner.BeginTransactionAsync(cancellationToken);
        public void CommitTransaction(string transactionId) => inner.CommitTransaction(transactionId);
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => inner.CommitTransactionAsync(transactionId, cancellationToken);
        public void RollbackTransaction(string transactionId) => inner.RollbackTransaction(transactionId);
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => inner.RollbackTransactionAsync(transactionId, cancellationToken);
        public void CreateSavepoint(string transactionId, string name) => inner.CreateSavepoint(transactionId, name);
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.CreateSavepointAsync(transactionId, name, cancellationToken);
        public void RollbackToSavepoint(string transactionId, string name) => inner.RollbackToSavepoint(transactionId, name);
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);
        public void ReleaseSavepoint(string transactionId, string name) => inner.ReleaseSavepoint(transactionId, name);
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);
        public bool SupportsSavepoints(string transactionId) => inner.SupportsSavepoints(transactionId);
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default) => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }

    public sealed class RecordingClient([FromKeyedServices] TransportInfoCarrierClient inner, PipelineCalls calls)
        : IInfoCarrierClient, IDisposable
    {
        public void Dispose() => Interlocked.Increment(ref calls.ClientDisposals);
        public IAsyncEnumerable<QueryStreamFrame> QueryDataStreamAsync(QueryDataRequest request, DbContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls.ClientRequests);
            return inner.QueryDataStreamAsync(request, context, cancellationToken);
        }
        public QueryDataResult QueryData(QueryDataRequest request, DbContext context) => inner.QueryData(request, context);
        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls.ClientRequests);
            return inner.QueryDataAsync(request, context, cancellationToken);
        }
        public SaveChangesResult SaveChanges(SaveChangesRequest request, DbContext context) => inner.SaveChanges(request, context);
        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, DbContext context, CancellationToken cancellationToken = default) => inner.SaveChangesAsync(request, context, cancellationToken);
        public TransactionResult BeginTransaction() => inner.BeginTransaction();
        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default) => inner.BeginTransactionAsync(cancellationToken);
        public void CommitTransaction(string transactionId) => inner.CommitTransaction(transactionId);
        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => inner.CommitTransactionAsync(transactionId, cancellationToken);
        public void RollbackTransaction(string transactionId) => inner.RollbackTransaction(transactionId);
        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default) => inner.RollbackTransactionAsync(transactionId, cancellationToken);
        public void CreateSavepoint(string transactionId, string name) => inner.CreateSavepoint(transactionId, name);
        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.CreateSavepointAsync(transactionId, name, cancellationToken);
        public void RollbackToSavepoint(string transactionId, string name) => inner.RollbackToSavepoint(transactionId, name);
        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);
        public void ReleaseSavepoint(string transactionId, string name) => inner.ReleaseSavepoint(transactionId, name);
        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default) => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);
        public bool SupportsSavepoints(string transactionId) => inner.SupportsSavepoints(transactionId);
        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default) => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }
}
