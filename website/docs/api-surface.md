# Public API

The surface an application touches is small. Everything else in the package is the provider's
internals, and you should not need to name it.

All types are in `InfoCarrier.Core` unless stated otherwise.

## Wiring

| Member | What it does |
|---|---|
| `DbContextOptionsBuilder.UseInfoCarrier(IInfoCarrierClient)` | Configures a context to remote its work through the given client. The whole client-side configuration. |
| `UseInfoCarrier(IInfoCarrierClient, Action<InfoCarrierDbContextOptionsBuilder>)` | The same, with what the client is told about the server: `AllowTypes`, `AllowArbitrarySqlExecution`, `UseNonRelationalServerStore`. See [Configuring the client](configuration/client.md#what-the-client-is-told-about-the-server). |
| `IServiceCollection.AddInfoCarrierAllowedTypes(params Type[])` | On the server. Admits CLR types a payload may name beyond the ones the model implies. |
| `IServiceCollection.AddInfoCarrierArbitrarySqlExecution()` | On the server. Lets a client send `FromSql` and `Database.SqlQuery<T>`. |
| `IServiceCollection.AddInfoCarrierServerTransactionTimeout(TimeSpan)` | On the server. Rolls back a transaction left idle that long. Off unless called. |
| `IServiceCollection.AddInfoCarrierServerLogForwarding(LogLevel)` | On the server. Sends the log events it raises back with the result. `AddInfoCarrierSensitiveServerLogForwarding` is the second grant a context with sensitive logging needs. |
| `IServiceCollection.AddInfoCarrierStandardValueMappers()` | Registers the value mappers for `IPAddress` and `Uri`. Automatic on the client; call it on the server yourself. |
| `IServiceCollection.AddEntityFrameworkInfoCarrier()` | Registers the provider's EF services. Only needed when you build EF's internal service provider yourself. |
| `IEndpointRouteBuilder.MapInfoCarrier(string pattern = "infocarrier")` | The server endpoint. In `InfoCarrier.Core.AspNetCore`. Returns `IEndpointConventionBuilder`. |
| `DatabaseFacade.UseInfoCarrierTransaction(IDbContextTransaction)` | Joins a transaction another context began. Non-owning. |

## The three replaceable objects

| Type | Implements | Constructor |
|---|---|---|
| `TransportInfoCarrierClient` | `IInfoCarrierClient` | `(IInfoCarrierTransport transport, IInfoCarrierSerializer serializer)` |
| `HttpInfoCarrierTransport` | `IInfoCarrierTransport` | `(HttpClient httpClient, IInfoCarrierSerializer serializer, string requestUri = "infocarrier")` |
| `SystemTextJsonInfoCarrierSerializer` | `IInfoCarrierSerializer` | `()` or `(InfoCarrierPayloadLimits limits)` |

## The server side

| Type | What it is |
|---|---|
| `InProcessInfoCarrierServer` | `IInfoCarrierServer` over a `DbContext` resolved from an `IServiceProvider`. Constructor: `(IServiceProvider serviceProvider)`. |
| `InfoCarrierEnvelopeServer` | Version check, dispatch, and turning a failure into a fault. Constructor: `(IInfoCarrierServer server, IInfoCarrierSerializer serializer)`; one method, `DispatchAsync`. Use it if you write a transport. |

## Interfaces you may implement

| Interface | Members | Page |
|---|---|---|
| `IInfoCarrierTransport` | `SendAsync(InfoCarrierEnvelope, CancellationToken)` | [Custom transports](configuration/transports.md) |
| `IInfoCarrierSerializer` | `Serialize<T>`, `Deserialize<T>`, and async counterparts | [Custom transports](configuration/transports.md#a-different-serializer) |
| `InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper` | `TryMapToWire`, `TryMapFromWire` | [Value mappers](configuration/value-mappers.md) |
| `IInfoCarrierServer` | The nine server operations | (none) |
| `IInfoCarrierClient` | The client half of the same nine | (none) |

`IInfoCarrierServer` and `IInfoCarrierClient` exist to be substituted in unusual hosting
arrangements. Most applications use `InProcessInfoCarrierServer` and `TransportInfoCarrierClient`
unchanged.

## Configuration types

| Type | Notes |
|---|---|
| `InfoCarrierPayloadLimits` | `(int? maxRequestBytes = DefaultMaxRequestBytes, int? maxResponseBytes = null)`. `Default` is the static instance; `DefaultMaxRequestBytes` is 64 MiB. `null` opts out of a limit. |

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
| `InfoCarrierMetrics` | The meter name and instrument names, as constants. See [Counting round trips](configuration/client.md#counting-round-trips). |

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
