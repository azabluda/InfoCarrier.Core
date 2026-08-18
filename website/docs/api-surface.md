# Public API

The surface an application touches is small. Everything else in the package is the provider's
internals, and you should not need to name it.

All types are in `InfoCarrier.Core` unless stated otherwise.

## Wiring

| Member | What it does |
|---|---|
| `DbContextOptionsBuilder.UseInfoCarrier(IInfoCarrierClient)` | Configures a context to remote its work through the given client. The whole client-side configuration. |
| `IServiceCollection.AddInfoCarrierStandardValueMappers()` | Registers the value mappers for `IPAddress` and `Uri`. Automatic on the client; **call it on the server**. |
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
| `IInfoCarrierServer` | The nine server operations | none |
| `IInfoCarrierClient` | The client half of the same nine | none |

`IInfoCarrierServer` and `IInfoCarrierClient` exist to be substituted in unusual hosting
arrangements. Most applications use `InProcessInfoCarrierServer` and `TransportInfoCarrierClient`
unchanged.

## Configuration types

| Type | Notes |
|---|---|
| `InfoCarrierPayloadLimits` | `(int? maxRequestBytes = DefaultMaxRequestBytes, int? maxResponseBytes = null)`. `Default` is the static instance; `DefaultMaxRequestBytes` is 64 MiB. `null` opts out of a bound. |

## Exceptions

| Type | Raised when |
|---|---|
| `InfoCarrierTransportException` | The request never reached a server, or what came back was not an envelope. |
| `InfoCarrierServerException` | The server's own exception type cannot be rebuilt here. Carries `ServerExceptionTypeName`. |

Anything else you catch is EF Core's own: `DbUpdateException`, `DbUpdateConcurrencyException`,
`InvalidOperationException`. See [Handling errors](guide/errors.md).

## Wire contracts

In `InfoCarrier.Core.Common`. You meet these only if you write a transport:

`InfoCarrierEnvelope` (with `ProtocolVersion`, `Operation`, `Payload`, `CorrelationId`, `Fault`),
`InfoCarrierOperation`, `InfoCarrierFault`, and the request/result records for queries, saves and
transactions.

They are serializable records with no behaviour. A transport moves an envelope; it does not
interpret one.

## Source documentation

Every public type carries XML documentation, so IntelliSense and the symbol package are the fastest
reference for anything not listed here. The packages ship symbols and SourceLink, so you can step
into the provider from a debugger.
