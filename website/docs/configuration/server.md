# Configuring the server

The server is an ordinary EF Core application. Add its base registrations, shared facts, and one
endpoint.

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlServer(connectionString));
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrier(builder => builder.ConfigureInfoCarrier());

WebApplication app = builder.Build();

app.MapInfoCarrier();

public static class ShopDeployment
{
    public static InfoCarrierDeploymentBuilder ConfigureInfoCarrier(
        this InfoCarrierDeploymentBuilder builder)
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = (string?)null });
}
```

| Registration | What it is for |
|---|---|
| `AddDbContext<ShopContext>` | Your context on your real provider. InfoCarrier never sees the connection string. |
| `AddScoped<DbContext>` | The server resolves the context per request as the base type, so the base type has to resolve. Without this line every request fails with a service-resolution error. |
| `IInfoCarrierSerializer` | The same format the client uses. Both ends must agree. |
| `IInfoCarrierServer` | `InProcessInfoCarrierServer` executes a request against a `DbContext` from this service provider. *In-process* means the same process as the database connection; it is the normal implementation, not a test double. |
| `AddInfoCarrier(...)` | Applies shared deployment facts and adds the standard value mappers. See [Value mappers](value-mappers.md). |

Use the same method in the client callback. It covers shapes, named types, mappers, and arbitrary
SQL. These settings cannot add provider support. The server controls admission.

`AddInfoCarrier` adds shared facts and standard mappers. It does not add the context, serializer,
server implementation, or endpoint.

## Migrating existing configuration

Version 10.3 warns about five planned version 11 removals:

| Existing method | Replacement |
|---|---|
| `InfoCarrierDbContextOptionsBuilder.AllowTypes(...)` | `InfoCarrierDeploymentBuilder.AddAllowedTypes(...)` |
| `InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution()` | `InfoCarrierDeploymentBuilder.AllowArbitrarySqlExecution(true)` |
| `AddInfoCarrierAllowedTypes(...)` | `AddAllowedTypes(...)` inside `AddInfoCarrier(...)` |
| `AddInfoCarrierArbitrarySqlExecution()` | `AllowArbitrarySqlExecution(true)` inside `AddInfoCarrier(...)` |
| `AddInfoCarrierStandardValueMappers()` | `AddInfoCarrier(...)` |

Version 10 continues to support these methods. The unreleased `UseRegisteredAnonymousShapes` and
prototype `AddInfoCarrierAnonymousShapes` methods were removed. Use shared `AddAnonymousShapes`.

Version 11 requires synchronous operations on all three remote-operation interfaces. Implementations
must keep synchronous and asynchronous input/output paths independent. The built-in HTTP transport
uses `HttpClient.Send` and synchronous response reads.

## The endpoint

```csharp
app.MapInfoCarrier();               // route: "infocarrier"
app.MapInfoCarrier("api/data");     // or your own
```

It returns an `IEndpointConventionBuilder`, so it takes conventions like any other endpoint:

```csharp
app.MapInfoCarrier()
   .RequireAuthorization("DataAccess")
   .RequireCors("client")
   .WithName("InfoCarrier");
```

The client's transport must name the same route. See
[Configuring the client](client.md#the-http-transport).

A malformed body, or a client speaking a different protocol version, is answered with `400` and a
plain-text message naming the problem, with no stack trace and no server paths. Anything the server
ran and that failed comes back as a fault inside a normal response. See
[Handling errors](../guide/errors.md).

## Payload limits

A server deserializes what an untrusted peer sent it, so this is the direction that matters. The
default is 64 MiB per request. Set your own if you know what your clients legitimately send:

```csharp
builder.Services.AddSingleton<IInfoCarrierSerializer>(
    _ => new SystemTextJsonInfoCarrierSerializer(
        new InfoCarrierPayloadLimits(maxRequestBytes: 8 * 1024 * 1024)));
```

A query tree is kilobytes, and a `SaveChanges` request is no bigger than the graph the client
tracked, so a low limit is usually safe. Cap the request bytes at your gateway too; this limit
catches whatever the gateway lets through.

## Granting what the model does not imply

Two deployment settings widen what a client may send. Both deny by default, and both are the
server's decision alone.

```csharp
builder.Services.AddInfoCarrier(builder => builder.ConfigureInfoCarrier());

public static class ShopDeployment
{
    public static InfoCarrierDeploymentBuilder ConfigureInfoCarrier(
        this InfoCarrierDeploymentBuilder builder)
        => builder
            .AddAllowedTypes(typeof(SqlServerDbFunctionsExtensions))
            .AllowArbitrarySqlExecution(true);
}
```

The first admits CLR types beyond those implied by your model. The usual reason is your provider's
`EF.Functions` family. Read [Security](../security.md) before adding other types.

The second admits `FromSql` and `Database.SqlQuery<T>`. One command text can run multiple
statements. Query filters do not apply. The database account's rights limit the caller.

## Registering anonymous projections

Register anonymous data structures in the shared deployment method using prototype objects:

```csharp
// Named records use the existing type permission registration.
builder
    .AddAnonymousShapes(
        new { Name = "", Age = 0 },
        new { Country = "", City = "" },
        new { CustomerId = 0, Year = 0 })
    .AddAllowedTypes(typeof(CustomerSummary));
```

Prototype values are ignored. Names, property order, and property types must match the client's
structure. The server and client can declare their anonymous objects in different assemblies.
Repeated calls compose. These registrations also cover grouping keys, join keys, and nested anonymous
data, not just final projections.

Nested anonymous objects are registered recursively. Arrays and generic collections can contain
them, including lists of lists. Each anonymous type has at most 32 properties; complete type
descriptors have at most 1,024 nodes and nesting depth 16. Each exchange admits at most 64 anonymous
identities. Rank-one non-vector arrays containing anonymous elements are unsupported.

The provider builds the immutable catalog using the registered server `DbContext` model.
`MapInfoCarrier()` constructs it during endpoint setup. An in-process server constructs it when
created. Registered structures reuse fixed generated types across exchanges. Keep registrations in
trusted startup code.

An unregistered shape reaching server execution is rejected. There is no automatic client-evaluation
fallback. Existing client projection reassembly still applies where the query planner chooses it.
Different original anonymous identities for the same registered structure cannot share one exchange;
the server rejects this case to preserve equality behavior and unambiguous response restoration.

Shape registration does not grant permission to its component types. Add custom components and
named records to the shared deployment method.
Records do not require anonymous-shape registration.
See [Security](../security.md) and the
[catalog contract](https://github.com/azabluda/InfoCarrier.Core/blob/main/docs/plans/v10/anonymous-shape-catalog.md)
for startup configuration and the limits.

## Sending the server's log to the client

EF writes its warnings about a query or a save on the server, so a client never sees them.
Forwarding is off until you grant it. Then they arrive in the client's own logger, under the
server's category and event id:

```csharp
builder.Services.AddInfoCarrierServerLogForwarding();   // warnings and above, the default
```

Pass a `LogLevel` to change it. At `Information` the SQL of every command the server runs goes too,
which tells a client your schema.

A server whose context enables sensitive data logging forwards nothing until you also call
`AddInfoCarrierSensitiveServerLogForwarding()`. That setting changes what EF's messages say
everywhere, so no rule can pick out the ones carrying values.

Model and context events never cross. Both halves build a model, and each logs its own.

## Context lifetime

`InProcessInfoCarrierServer` takes a fresh scope per request, so every request gets a clean change
tracker. A client request is self-contained, and leftover tracked entities from a previous request
would collide with the next one.

The exception is a transaction, and it is the one case where server state outlives a request.
`BeginTransaction` pins one context and its connection until the commit or rollback, on the instance
that minted the token, so a load-balanced deployment needs session affinity for the life of a
transaction. [Transactions](../guide/transactions.md) has that and what an abandoned one costs.

## Evicting an abandoned transaction

A client that vanishes mid-transaction leaves the server holding a context and a connection; its
own `DisposeAsync` cannot help, because the process is gone. Tell the server how long to wait.

```csharp
builder.Services.AddInfoCarrierServerTransactionTimeout(TimeSpan.FromMinutes(10));
```

**Off until you call this**, so upgrading changes nothing. Every request naming the token refreshes
the clock, so the value bounds idleness rather than duration.

An eviction rolls the transaction back and logs at `Warning`. **A later commit then fails rather
than reporting success**, because the work is gone. A rollback stays silent, so disposing after a
commit still behaves.

## Binding a transaction to its caller

The token that names a transaction is a bearer credential: whoever holds it can query and save
inside that transaction, not merely end it. Tell the server who a caller is, and it refuses a token
opened by somebody else.

```csharp
builder.Services.AddInfoCarrierHttpCallerIdentity(http => http.User.FindFirst("sub")?.Value);
```

**Off until you call this, and it is a second lock rather than the first.** Without
`RequireAuthorization` on the endpoint every caller is anonymous, and every anonymous caller
matches.

**Choose a value that stays the same for as long as a transaction lives**, because a caller whose
value changes mid-transaction loses its own work. You pick it: a subject id, a login name and a
tenant claim behave differently under a token refresh.

Nothing changes on the wire, so an existing client needs no rebuild.

## Saying the store is a document store

MongoDB, Cosmos DB and anything else whose unit of write is a whole record keep an owned type
inside its owner. There is no partial write. Changing a customer's name rewrites the customer
document, so an address the change set never mentioned is written out of existence: the save
reports success and the next read fails on a field that is gone.

```csharp
builder.Services.AddInfoCarrierServerDocumentStore();
```

The server then reads the stored document and puts back whatever the change set left out.
**Off until you call this.** Do not call it for a relational store, where an owned type is columns
of the owner's row or a row of its own, and neither can go missing.

The client has a switch of its own, `UseNonRelationalServerStore()`, which sends the whole document
and saves the server that read. **Set both, and rely on this one.** A client can only send what its
change tracker holds, so a stub it attached carries a bare root even with the switch on, and a
deployment that never set the switch carries one always.

The read happens only where something could be missing. A change set that names every owned
navigation the model declares is written as it stands, and an insert or a delete is never read at
all.

## Where the checks go

A global query filter on the server's model applies to every query by default, which is what you
want for your own honest client. **It is not a control.** `IgnoreQueryFilters()` is an ordinary EF
Core operator: it travels in the expression tree like any other, and the server honours it. No query
filter reaches `SaveChanges`, so a client can submit a row whose key belongs to someone else.

So put a write check in the server's `SaveChanges` override or an EF interceptor, and a read check
in a query interceptor, which sees the client's tree before EF translates it.
[Multi-tenancy](../multi-tenancy.md) works both of them through, and [Security](../security.md) has
the threat model.

Keep the filter anyway, as the default for your own client:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>().HasQueryFilter(o => o.TenantId == _tenant.Current);
}
```

Everything else you register on the server's context runs too: EF interceptors, `SaveChanges`
overrides, auditing, soft-delete conventions. The client's request is work arriving at your context.
Nothing registered on the client reaches the server, so anything a client's own model declares is a
convenience.

## Model parity

Both halves build a model from the same `DbContext` source, and the wire names entity types and
properties, so deploy them together: a property the client names and the server does not know is a
failed request. Enable a model-shaping option on both sides or neither. A browser client is the
deliberate exception, covered on the [Blazor WebAssembly](../platforms/blazor-webassembly.md) page.
