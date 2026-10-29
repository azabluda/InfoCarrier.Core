# Public API

The surface an application touches is small. Everything else in the package is the provider's
internals, and you should not need to name it.

All types are in `InfoCarrier.Core` unless stated otherwise.

## Wiring

| Member | What it does |
|---|---|
| `IServiceCollection.AddInfoCarrierClient(Action<InfoCarrierClientOptions>)`, `AddInfoCarrierClient(string clientName, Action<InfoCarrierClientOptions>)` | Registers the unnamed default or a named client. Returns `InfoCarrierClientBuilder`, which implements `IHttpClientBuilder` for handler configuration. |
| `ReplaceService<TService, TImplementation>()`, `ReplaceService<TService, TCurrentImplementation, TNewImplementation>()` | Replaces supported registration-local implementations while preserving lifetime. Clients support client, serializer, and transport contracts; servers support server, serializer, and pipeline-factory contracts. See [component replacement](configuration/transports.md#replacing-registered-components). |
| `InfoCarrierClient.Create(HttpClient, Action<InfoCarrierClientOptions>)` | Creates the default HTTP client directly. The caller owns the supplied `HttpClient`. |
| `InfoCarrierClient.Configure(IInfoCarrierClient, Action<InfoCarrierClientOptions>)` | Applies client and shared deployment options to a custom client. |
| `DbContextOptionsBuilder.UseInfoCarrier()`, `UseInfoCarrier(string clientName)` | Attaches the unnamed default or named client. Pass `IInfoCarrierClient` explicitly for manual options or custom factories. |
| `IServiceCollection.AddInfoCarrierServer<TContext>(Action<InfoCarrierServerOptions>?)`, `AddInfoCarrierServer<TContext>(string serverName, Action<InfoCarrierServerOptions>?)` | Registers the unnamed default or a named server. A further overload accepts a scoped context resolver. See [server configuration](configuration/server.md#selecting-a-scoped-context). |
| `InfoCarrierServerBuilder.AddHttpCallerIdentity(Func<HttpContext, string?>)` | From `InfoCarrier.Core.AspNetCore`. Binds transactions from that server registration to their caller. Off unless called. |
| `IServiceCollection.AddEntityFrameworkInfoCarrier()` | Registers the provider's EF services. Only needed when you build EF's internal service provider yourself. |
| `IEndpointRouteBuilder.MapInfoCarrier(string pattern = "infocarrier")`, `MapInfoCarrier(string pattern, string serverName)`, `MapInfoCarrier(InfoCarrierServerBuilder server, string pattern)`, `MapInfoCarrier<TContext>(string pattern)` | Maps the default, named, retained, or unique context registration. In `InfoCarrier.Core.AspNetCore`. Returns `IEndpointConventionBuilder`. |
| `DatabaseFacade.UseInfoCarrierTransaction(IDbContextTransaction)` | Joins a transaction another context began. Non-owning. |

`InfoCarrierProtocolException` reports incompatible wire contracts through `ClientVersion` and
`ServerVersion`. Retry cannot fix it. See [handling errors](guide/errors.md#incompatible-client-and-server).

## The three replaceable objects

| Type | Implements | Constructor |
|---|---|---|
| `TransportInfoCarrierClient` | `IInfoCarrierClient` | `(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer)` |
| `HttpInfoCarrierTransport` | `IInfoCarrierTransport` | `(HttpClient httpClient, IInfoCarrierSerializer serializer, string requestUri = "infocarrier")` |
| `SystemTextJsonInfoCarrierSerializer` | `IInfoCarrierSerializer` | `()` or `(InfoCarrierPayloadLimits limits)` |

## The server side

| Type | What it is |
|---|---|
| `InProcessInfoCarrierServer` | `IInfoCarrierServer` over a `DbContext` resolved from an `IServiceProvider`. Direct constructor: `(IServiceProvider serviceProvider, InfoCarrierServerOptions? options = null)`. Settings are snapshotted. |
| `InfoCarrierEnvelopeServer` | Version check, dispatch, and turning a failure into a fault. Constructor: `(IInfoCarrierServer server, IInfoCarrierSerializer serializer)`; `Dispatch` and `DispatchAsync`. Use it if you write a transport. |

## Interfaces you may implement

| Interface | Members | Page |
|---|---|---|
| `IInfoCarrierTransport` | `Send` and `SendAsync` | [Custom transports](configuration/transports.md) |
| `IInfoCarrierSerializer` | `Serialize<T>`, `Deserialize<T>`, and async counterparts | [Custom transports](configuration/transports.md#a-different-serializer) |
| `InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper` | `TryMapToWire`, `TryMapFromWire` | [Value mappers](configuration/value-mappers.md) |
| `IInfoCarrierServer` | The nine server operations | [Replacements](configuration/transports.md#client-server-and-pipeline-replacements) |
| `IInfoCarrierClient` | The client half of the same nine | [Replacements](configuration/transports.md#client-server-and-pipeline-replacements) |

`IInfoCarrierServer` and `IInfoCarrierClient` exist to be substituted in unusual hosting
arrangements. Most applications use `InProcessInfoCarrierServer` and `TransportInfoCarrierClient`
unchanged.

`IInfoCarrierStreamServer` adds streamed query frames. `IExpressionSerializer` and
`InfoCarrier.Core.Expressions.IDynamicValueMapper` cover expression translation, execution policy,
row mapping, and saves. Client replacements use Entity Framework's options builder.
`IInfoCarrierServerPipelineFactory` creates operation-owned `IInfoCarrierServerPipeline` instances
against the actual server context. See [pipeline replacements](configuration/transports.md#client-server-and-pipeline-replacements).

`GetInfoCarrierClient` selects a complete client. `GetInfoCarrierServerEndpoint` selects a
host-owned server/serializer pair by default, name, handle, or unique context type for custom hosting.
`InfoCarrierClientSettings` supplies immutable construction settings through inherited-key injection.

## Configuration types

| Type | Notes |
|---|---|
| `InfoCarrierClientOptions` | Endpoint, media type, response limit, server query rules, and shared deployment settings. |
| `InfoCarrierServerOptions` | Request limit, media type, transaction timeout, document storage, log forwarding, and shared deployment settings. |
| `InfoCarrierLogForwardingOptions` | Minimum level and the separate sensitive-data grant. Available through `InfoCarrierServerOptions.LogForwarding`. |
| `InfoCarrierDeploymentBuilder` | Configures shared shapes, types, mappers, and arbitrary SQL. In `InfoCarrier.Core.Abstractions`. |
| `InfoCarrierDeploymentBuilderExtensions` | Generic shared methods preserve the concrete builder type throughout fluent chains. |
| `ConfigureShared(Action<InfoCarrierDeploymentBuilder>)` | Applies an ordinary shared method immediately at its position in the role callback, returning the same concrete builder. |
| `InfoCarrierRoleOptionsExtensions` | `WithEndpoint`, `WithMaxResponseBytes`, `WithServerUsesRelationalQueryRules`, `WithMaxRequestBytes`, `WithTransactionIdleTimeout`, `WithStoresOwnedDataInsideOwner`, and `WithLogForwarding` configure the same mutable options properties. |
| `InfoCarrierPayloadLimits` | `(int? maxRequestBytes = DefaultMaxRequestBytes, int? maxResponseBytes = null)`. `Default` is the static instance; `DefaultMaxRequestBytes` is 64 MiB. `null` opts out of a limit. |
| `InfoCarrier.Core.Expressions.AnonymousShapeCatalog` | Low-level immutable structural catalog. Applications register prototypes through `AddAnonymousShapes`. See [server configuration](configuration/server.md#registering-anonymous-projections). |

## Exceptions

| Type | Raised when |
|---|---|
| `InfoCarrierTransportException` | The request never reached a server, or what came back was not an envelope. |
| `InfoCarrierServerException` | The server's own exception type cannot be rebuilt here. Carries `ServerExceptionTypeName`. |

Anything else you catch is EF Core's own: `DbUpdateException`, `DbUpdateConcurrencyException`,
`InvalidOperationException`. See [Handling errors](guide/errors.md).

## Diagnostics

| Type | Notes |
|---|---|
| `InfoCarrierEventId` | Holds `QuerySplit`, the one event this provider raises. Pass it to `LogTo` or `ConfigureWarnings` like any EF Core event id. See [Logging](configuration/client.md#logging). |
| `InfoCarrierMetrics` | Meter, instrument, and tag constants for client attempts and HTTP server requests. See [Counting round trips](configuration/client.md#counting-round-trips) and [server requests](configuration/server.md#counting-server-requests). |

## Wire contracts

In `InfoCarrier.Core.Common`. You meet these only if you write a transport:

`InfoCarrierEnvelope` (with `ProtocolVersion`, `Operation`, `Payload`, `CorrelationId`, `Fault`),
`InfoCarrierOperation`, `InfoCarrierFault`, and the request/result records for queries, saves and
transactions.

They are serializable records with no behaviour. A transport moves an envelope; it does not
interpret one.

## Source documentation

Every public type carries XML documentation, so IntelliSense is the fastest reference for anything
not listed here. The packages ship symbols and SourceLink, so you can step into the provider from a
debugger.
