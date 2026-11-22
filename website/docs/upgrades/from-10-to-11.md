# Upgrade from 10.3 to 11

Version 11.0.0 is a breaking upgrade from 10.3. Updating package references alone is not sufficient.
Install .NET 11 and Entity Framework Core 11, then update both InfoCarrier packages.
See [installation](../getting-started/installation.md) for package setup.

Keep a working 10.3 application and its data backup while testing the upgrade.
Plan client and server changes together. Version 11 does not support version 10 clients.

## Plan the breaking changes

| Your application uses | What must change |
|---|---|
| Version 10 configuration APIs | Replace registration methods and option callbacks using the table below. |
| Extra types or anonymous shapes in server-executed expressions | Register them on both sides; implicit local execution is no longer a fallback. |
| Local filtering, ordering, grouping, joins, set operations, or equality inside a remote query | Rewrite for the backend, or choose local execution explicitly with `AsEnumerable`. |
| Separately deployed clients and servers | Coordinate the major-version upgrade; do not leave version 10 clients connected to a version 11 server. |
| A custom transport, client, server, or serializer | Implement current contracts, including synchronous operations, and handle the changed query/result payloads. |
| Non-tracking asynchronous results | Handle partial results, cancellation, late failures, and early enumeration disposal. |
| Client database setup calls | Move schema creation, deletion, and connectivity checks to the server. |
| Custom logging or provider components | Replace removed APIs and rebuild against the new constructor and interface signatures. |
| Precompiled shared configuration libraries | Rebuild them; unchanged source syntax does not establish binary compatibility. |

## Replace application setup

| Version 10.3 | Version 11 |
|---|---|
| `AddInfoCarrier(...)` | `AddInfoCarrierClient` and `AddInfoCarrierServer<TContext>` |
| `InfoCarrierDbContextOptionsBuilder` | `InfoCarrierClientOptions` |
| `AllowTypes`, `AddInfoCarrierAllowedTypes` | `AddAllowedTypes` in shared configuration on both sides |
| `AddInfoCarrierArbitrarySqlExecution` | `AllowArbitrarySqlExecution(true)` on both sides; the server controls permission |
| `AddInfoCarrierStandardValueMappers` | Standard mappers are automatic; add custom instances through `AddValueMappers` |
| `UseNonRelationalServerStore()` | Client `ServerUsesRelationalQueryRules = false` |
| `AddInfoCarrierServerDocumentStore` | Server `StoresOwnedDataInsideOwner = true` |
| `AddInfoCarrierServerTransactionTimeout` | Server `TransactionIdleTimeout` |
| Old log-forwarding registrations | Server `LogForwarding` or `WithLogForwarding` |
| `AddInfoCarrierHttpCallerIdentity` on services | `AddHttpCallerIdentity` on the selected server builder |
| `InfoCarrierOptionsExtension.WithAllowedTypes`, `WithArbitrarySqlExecution` | Configure client/server options through their callbacks |

Configuration-only marker interfaces were removed.
The two document-store settings serve different purposes; [configure both](../applications/configuration/document-stores.md).

A shared method replaces scattered settings:

```csharp
public static class ShopDeployment
{
    public static void ConfigureInfoCarrier(InfoCarrierDeploymentBuilder builder)
        => builder.AddAnonymousShapes(new { Id = 0, CustomerId = "" });
}

// Client application
builder.Services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureInfoCarrier));
builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());

// Server application, after registering its real database context
builder.Services.AddInfoCarrierServer<ShopContext>(o =>
    o.ConfigureShared(ShopDeployment.ConfigureInfoCarrier));
```

Imports and full model code are in the [first tutorial](../getting-started/first-app.md).
Overloads accepting an explicit client remain available.
Several shared-builder methods became extension methods. Recompile custom libraries even when
their source calls look unchanged.

## Register types and anonymous shapes used by server execution

Version 10 could keep some grouping, joins, or comparisons on the client when their types could not
cross the connection. Version 11 refuses that implicit fallback. Such a query can now throw
`InvalidOperationException` before reading database rows.

| Type or query shape | What to do |
|---|---|
| Entities, mapped property types, and other types already known from the model | No extra registration is needed for ordinary model queries. |
| An additional named key, helper, or result type needed by server execution | Add it through `AddAllowedTypes(typeof(...))` on both sides. |
| An anonymous structure needed by server execution | Add a matching prototype through `AddAnonymousShapes(new { ... })` on both sides. |
| A supported final projection constructed on the client | It can remain unregistered, including an anonymous result. |
| An anonymous composite join key handled by a safe internal rewrite | It can still reach the server without an explicit shape registration. |

For example, declaring `new { CustomerId = "", Year = 0 }` registers that property's names, order,
and types. It does not register every anonymous object in the application. A different structure
needed by the server requires its own prototype. Custom member types need their own permission too.

Apply registrations through the same `ConfigureShared` method in both applications.
Registration permits transport; it does not make a helper or key translatable by the database provider.
See [shared settings](../applications/configuration/shared.md) for complete registration examples.

## Check query behavior

A filter, order, grouping, join, distinctness, set operation, or equality operation that cannot run
on the server now fails unless you explicitly select local execution.
Do not depend on automatic local execution.
Register shared key/helper types where appropriate, then check the database provider's translation.
Registration alone does not make a query supported.

For intentional local work, reduce the data on the server first and call `AsEnumerable`
before the local operation. Supported final client projections remain available.
See [query execution](../applications/querying.md).

Non-tracking asynchronous queries can now return partial results before a later error.
Do not assume a failed enumeration produced no rows or that earlier row-processing effects were undone.
Tracked and single-result operations stay buffered.
Review cancellation, early disposal, and response limits using the [streaming guide](../applications/streaming.md).

Concatenated client projections can also stream progressively. Projections followed by range paging,
such as `Take(1..3)`, can require buffering. Check first-row latency as well as final query results.

## Check document writes and generated values

For a document database, configure both [document-store settings](../applications/configuration/document-stores.md).
Check updates that change only an owned object, replace collection elements, or reorder a stored collection.
Version 11 preserves returned owner concurrency values and generated collection keys in these saves.
If you use `SaveChanges(false)`, call `ChangeTracker.AcceptAllChanges()` after your application accepts the save.

Key generation follows the server's model and provider. A temporary client key does not make a
non-generated server key generate automatically. Verify saved keys and dependent foreign keys
through a fresh server context. Version 11 document validation uses Cosmos; it does not establish
compatibility with a MongoDB provider for Entity Framework Core 11.

## Move database setup to the server

Client `EnsureCreated`, `EnsureDeleted`, and `CanConnect` calls no longer report success.
They fail because the client does not own the database.
Create schema and check connectivity through the server's database provider.

## Rebuild custom components

- Implement both `Send` and `SendAsync` in custom transports. Custom clients and servers must also
  implement their synchronous operation members. Keep synchronous calls synchronous; backend restrictions still apply.
- Review every member of custom client and server interfaces, including streamed results.
  Streaming interfaces are optional; buffered adapters do not establish progressive delivery.
- Use [service replacement](../extending/services.md) on the intended client or server builder.
- Change static `ServerLogCapture.Begin` calls to calls on the capture instance used by the host.
- Review query-split event handlers: `UnregisteredKeyTypes` was removed.
  Use current event data and handle refused queries. Replace count-only `QuerySplit` calls with the
  overload that also supplies the client remainder expression.
- `InfoCarrierQueryableMethodTranslatingExpressionVisitorFactory`,
  `InfoCarrierShapedQueryCompilingExpressionVisitorFactory`, and `InfoCarrierDatabaseCreator` were removed.
  Remove custom registrations of these types; ordinary application setup needs no replacement.
- `InfoCarrierTypeMappingSource` was removed. Custom mapping-source subclasses should derive from
  `InfoCarrier.Core.Relational.InfoCarrierRelationalTypeMappingSource` and accept both core and
  relational mapping dependencies. Update `FindMapping` overrides to use `RelationalTypeMappingInfo`
  and return `RelationalTypeMapping`. Ordinary provider registration already uses this service.

For direct constructors and serializer/value-mapper implementations, compare the current signatures
with your code. A new optional parameter can preserve a source call while changing the compiled signature.
`IExpressionSerializer` and `IDynamicValueMapper` have additional required members for execution
settings, server context, type catalogs, reference tracking, and result/save mapping.
Update implementations rather than keeping a version 10 binary behind a version 11 registration.
The old `IInfoCarrierValueMapper` type forwarder from Core to Abstractions was also removed;
rebuild custom mapper libraries against the current packages.
The [API index](../reference/api.md) and IntelliSense identify the current contracts.

## Check custom serialization and deployment

Protocol 4 uses typed query content and buffered rows.
A custom serializer must handle those objects. Streamed messages keep their fixed JSON format.
See [serialization](../extending/serialization.md).

Custom hosts and transports must preserve explicit request and response protocol identification.
Missing identification is refused instead of being interpreted as the current version.
Streamed HTTP requests use `InfoCarrier-Stream-Operation: Query`; the old `InfoCarrier-Operation`
selector is rejected. See the [custom transport requirements](../reference/compatibility.md#if-you-write-a-custom-transport).

Protocol 3 conversion exists, but does not extend support to version 10 packages.
Test the actual package combinations you will deploy.
See [compatibility](../reference/compatibility.md).

## Verify before switching users

Build both applications and custom libraries.
Run representative reads, writes, relationships, transactions, and error recovery against a test database.
Check effects through a fresh context on the server.
Verify authentication, tenant restrictions, limits, and disposal.
Exercise your application's configuration and any custom transports, rather than checking only renamed methods.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
