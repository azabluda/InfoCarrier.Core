# Upgrade from 10.3 to 11

This guide targets the version 11 development source. It currently uses Entity Framework Core 10.
Use the [source installation instructions](../getting-started/installation.md); stable 10.3 packages
do not contain these setup methods.

Keep a working 10.3 application and its data backup while testing the upgrade.
Plan client and server changes together. Version 11 does not support version 10 clients.

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

## Check query behavior

A filter, order, grouping, join, or equality operation that cannot run on the server now fails.
Do not depend on automatic local execution.
Register shared key/helper types where appropriate, then check the database provider's translation.
Registration alone does not make a query supported.

For intentional local work, reduce the data on the server first and call `AsEnumerable`
before the local operation. Supported final client projections remain available.
See [query execution](../applications/querying.md).

Non-tracking asynchronous queries can now return partial results before a later error.
Tracked and single-result operations stay buffered.
Review cancellation, early disposal, and response limits using the [streaming guide](../applications/streaming.md).

## Move database setup to the server

Client `EnsureCreated`, `EnsureDeleted`, and `CanConnect` calls no longer report success.
They fail because the client does not own the database.
Create schema and check connectivity through the server's database provider.

## Rebuild custom components

- Implement both `Send` and `SendAsync` in custom transports. Keep synchronous calls synchronous.
- Review every member of custom client and server interfaces, including streamed results.
- Use [service replacement](../extending/services.md) on the intended client or server builder.
- Change static `ServerLogCapture.Begin` calls to calls on the capture instance used by the host.
- Review query-split event handlers: `UnregisteredKeyTypes` was removed.
  Use current event data and handle refused queries.
- Removed obsolete query translation/shaped-query factories need no replacement in ordinary application setup.

For direct constructors and serializer/value-mapper implementations, compare the current signatures
with your code. A new optional parameter can preserve a source call while changing the compiled signature.
The [API index](../reference/api.md) and source XML comments identify the current contracts.

## Check custom serialization and deployment

Protocol 4 uses typed query content and buffered rows.
A custom serializer must handle those objects. Streamed messages keep their fixed JSON format.
See [serialization](../extending/serialization.md).

Protocol 3 conversion exists, but does not extend support to version 10 packages.
Test the actual package combinations you will deploy.
See [compatibility](../reference/compatibility.md).

## Verify before switching users

Build both applications and custom libraries.
Run representative reads, writes, relationships, transactions, and error recovery against a test database.
Check effects through a fresh context on the server.
Verify authentication, tenant restrictions, limits, and disposal.
Compile the complete configuration and transport examples rather than checking only renamed methods.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
