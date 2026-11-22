# Use a document database

A document database can store owned objects inside the owner's record.
Updating a partially loaded owner can lose nested data unless the server completes that data before saving.

Configure both sides:

```csharp
// Client
builder.Services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .WithServerUsesRelationalQueryRules(false));

// Server, after registering ShopContext with the chosen document provider
builder.Services.AddInfoCarrierServer<ShopContext>(o =>
    o.WithStoresOwnedDataInsideOwner(true));
```

The client setting disables relational-only query checks and includes tracked owned data in writes.
The server setting loads owned data that the submitted changes omitted.

## Why both are needed

Suppose a stored customer has an address. The client attaches only a customer stub and changes its name.
Its tracker does not contain the address, so the client cannot send it, even with its setting enabled.
The server must read the missing owned data before writing the document.

Completion is needed only where an update omits an owned navigation declared by the model.
An insert or delete does not trigger this read. A complete change set does not need it either.

Both settings default to their relational behavior: relational query rules on the client and document
completion off on the server. Do not enable document completion for a relational store.
Entity Framework Core's InMemory provider is not a document database merely because it is non-relational.

## Save nested changes

Changing an owned object can also change its owner's generated concurrency value.
After a successful save, InfoCarrier applies that value to the tracked owner even when the owner
had no direct property edits. This includes deferred acceptance with `SaveChanges(false)` followed
by `ChangeTracker.AcceptAllChanges()`.

Owned collections are rebuilt in their current navigation order. Generated element keys returned
by the provider are applied to the corresponding tracked elements, including replacements and reordering.
Whether the database persists collection order depends on its mapping and provider.

Read the document through a fresh server context when checking stored nested values, order, and concurrency.
See [saving generated values](../saving.md#store-generated-values-come-back).

## Provider limits

Version 11 document reads and writes are verified with the Entity Framework Core 11 Cosmos provider.
MongoDB has not been validated with this release.
Use a document provider compatible with EF Core 11 and validate it against your application.
A setting cannot add a query or transaction feature that the underlying provider lacks.
Verify the provider version, model, and write behavior used by your application.

Provider-specific query methods need their declaring types registered in shared configuration.
The server must also have the provider and any required model settings.
For example, Cosmos partition-key queries retain Cosmos's partition rules and asynchronous execution requirements.
See [provider functions and raw queries](../querying.md#what-is-not-part-of-the-surface).

Do not assume that several document writes are one atomic save on every provider.
In particular, the [write-recovery procedure](../../operations/write-recovery.md) explains why its
marker pattern requires all changes to share one transaction, including with Cosmos DB batches.

See [query rules](../querying.md#rules-that-come-from-the-servers-store) and
[shared settings](shared.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
