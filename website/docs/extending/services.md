# Replace services and pipelines

Use client and server builders to replace components for a selected registration.
Choose the extension point that matches the requirement:

| Requirement | Where to start |
|---|---|
| Headers, authentication, or HTTP handlers | [Client HTTP configuration](../applications/configuration/client.md#configure-http) |
| A different application value representation | [Value mappers](value-mappers.md) |
| A different message format | [Serialization](serialization.md) |
| A different network protocol | [Transports](transports.md) |
| Replace an InfoCarrier host component | The builder examples below |
| Replace Entity Framework provider behavior | [Provider services](#entity-framework-provider-services) |

The complete `LoggingSerializer` example is on the [serialization page](serialization.md).

Replace a serializer for one client without changing other clients:

```csharp
services.AddInfoCarrierClient("orders", o => o.WithEndpoint("https://orders.example.com/infocarrier"))
    .ReplaceService<IInfoCarrierSerializer, LoggingSerializer>();

services.AddInfoCarrierClient("catalog", o => o.WithEndpoint("https://catalog.example.com/infocarrier"));
```

In the orders server application, select the compatible serializer on its server registration:

```csharp
var orders = services.AddInfoCarrierServer<ShopContext>("orders")
    .ReplaceService<IInfoCarrierSerializer, LoggingSerializer>();

app.MapInfoCarrier(orders, "api/orders");
```

This works with unnamed, named, retained-handle, and typed endpoint selection. Context injection
does not change: `UseInfoCarrier("orders")` selects the client, while application code uses its context.

`ReplaceService<TService, TImplementation>()` preserves the service's lifetime, as Entity Framework's
method does. `ReplaceService<TService, TCurrentImplementation, TNewImplementation>()` targets an
original implementation. A matching targeted choice takes precedence over a contract-wide choice.
Repeated calls for the same service and original implementation use the last choice. There is no
lifetime argument or construction lambda.

Client registrations support `IInfoCarrierClient`, `IInfoCarrierSerializer`, and
`IInfoCarrierTransport`. Server registrations support `IInfoCarrierServer`, `IInfoCarrierSerializer`,
and `IInfoCarrierServerPipelineFactory`. Other contracts fail during configuration.
Use Entity Framework's own options-builder `ReplaceService` for its internal provider services.

The host constructs replacements and supplies their constructor dependencies. No separate
`AddSingleton<LoggingSerializer>()` is needed. These components retain singleton lifetime and must
support concurrent calls. The host disposes them; a transport must not dispose its injected serializer.

For a replacement transport, select dependencies from the same registration.
This constructor fragment illustrates selection; `OrdersTransport` must implement both transport methods:

```csharp
services.AddInfoCarrierClient("orders", o => o.WithEndpoint(ordersEndpoint))
    .ReplaceService<IInfoCarrierSerializer, LoggingSerializer>()
    .ReplaceService<IInfoCarrierTransport, OrdersTransport>();

// Constructor of an application implementation of IInfoCarrierTransport:
public OrdersTransport(
    [FromKeyedServices] IInfoCarrierSerializer serializer,
    [FromKeyedServices] InfoCarrierClientSettings settings,
    ILogger<OrdersTransport> logger)
{
    // settings contains this client's name, endpoint, response limit, and media type.
    // Implement Send and SendAsync for the application's protocol.
}
```

Import `Microsoft.Extensions.DependencyInjection` for the annotation. Parameterless
`[FromKeyedServices]` selects the serializer from the transport's own registration. Unannotated
dependencies use ordinary host resolution; they do not inherit its key. Do not put public registration
names in these constructors. Custom channel services can use ordinary host injection.

The client builder still implements `IHttpClientBuilder`, so existing handler configuration chains
work. A replacement transport does not activate that private HTTP client or its handler callbacks.
Use custom protocol hosting on the server; `MapInfoCarrier` remains HTTP hosting.
A custom transport can omit the endpoint URI. The default HTTP transport requires an absolute
endpoint when it activates. Supplied relative URIs fail during registration. Configure application
channel dependencies through host services.

Registration limits wrap custom deserialization too. Selected server limits also guard decoded query
expressions, including custom hosting. Streamed response limits include all framing.
Protocol 4 query bodies contain typed expression objects, and buffered results contain typed rows.
A custom serializer encodes those objects with its operation payload. Serving contract 3 also
requires its historical request and response types. Stream frames retain JSON encoding; a different
streaming format needs a separate adapter.
Set `MediaType` in the client and server options for custom envelope/payload encoding.
Its default is `application/json`. Stream response frames retain their JSON content type.

## Client, server, and pipeline replacements

The following constructor fragments show dependency selection. They are not complete implementations.
Each replacement must implement every member of its interface.

A client or server replacement can wrap the built-in implementation through inherited-key injection:

```csharp
services.AddInfoCarrierClient("orders", configureOrders)
    .ReplaceService<IInfoCarrierClient, AuditedClient>();

// AuditedClient implements IInfoCarrierClient and borrows this dependency:
public AuditedClient([FromKeyedServices] TransportInfoCarrierClient inner) { /* retain inner */ }

services.AddInfoCarrierServer<ShopContext>("orders")
    .ReplaceService<IInfoCarrierServer, AuditedServer>();

// AuditedServer implements IInfoCarrierServer and borrows this dependency:
public AuditedServer([FromKeyedServices] InProcessInfoCarrierServer inner) { /* retain inner */ }
```

Implement `IInfoCarrierStreamServer` on a streaming server replacement. HTTP hosting detects this
interface; otherwise, it adapts a buffered query result. Never dispose borrowed built-in services.
Registered clients retain deployment settings and one client measurement around the replacement.
HTTP endpoint measurements surround the selected server. A complete client or server replacement owns its execution
semantics, including admission, transactions, and cancellation; a forwarding wrapper preserves them.

Client expression services use standard Entity Framework replacement:

```csharp
services.AddDbContextFactory<ShopContext>(options => options
    .UseInfoCarrier("orders")
    .ReplaceService<IExpressionSerializer, OrdersExpressionSerializer>()
    .ReplaceService<IDynamicValueMapper, OrdersValueMapper>());
```

Import `InfoCarrier.Core.Expressions` for the mapper interface. These services retain scoped lifetime;
Entity Framework includes replacement choices in its provider cache. The serializer's `ValueMapper`
must be the same mapper used for expression values, result rows, and saves. Interfaces include the
execution-policy, reference-scope, and materialization operations; subclassing built-ins is optional.

The server builds pipelines against its actual leased context, which may retain a transaction:

```csharp
services.AddInfoCarrierServer<ShopContext>("orders")
    .ReplaceService<IInfoCarrierServerPipelineFactory, OrdersPipelineFactory>();
```

The singleton factory's `Create` receives that context, the selected allowed types, ordered value
mappers, and trusted anonymous-shape catalog. Return a fresh `IInfoCarrierServerPipeline` for each
operation. Its `Serializer.ValueMapper` serves translation, rows, and saves. The operation disposes
the pipeline after success, failure, cancellation, or stream disposal. The pipeline owns any components
it creates. The host owns the supplied context. Support both synchronous and asynchronous disposal.
Do not retain contexts in the factory
or resolve a second context. Custom translation must preserve the supplied admission policy.

## Entity Framework provider services

Transport and payload serializer replacement uses the host registration shown above.
Entity Framework's own provider services use its context options instead.

Register application [value mappers](value-mappers.md) with `AddValueMappers` in the client role
callback. Build EF's internal service provider yourself only when replacing a provider service or
assembling a custom provider configuration.

```csharp
ServiceProvider providerServices = new ServiceCollection()
    .AddEntityFrameworkInfoCarrier()
    .AddSingleton<IInfoCarrierValueMapper, MoneyValueMapper>()
    .BuildServiceProvider();

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInternalServiceProvider(providerServices)
    .UseInfoCarrier(client)
    .Options;
```

`AddEntityFrameworkInfoCarrier()` registers the provider and its standard value mappers. Build and
share this provider once. Otherwise, use the default provider.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
