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

## Provider limits

Owned-document behavior is tested against MongoDB alongside tests using that provider directly.
A setting cannot add a query or transaction feature that the underlying provider lacks.
Verify the provider version, model, and write behavior used by your application.

Do not assume that several document writes are one atomic save on every provider.
In particular, the [write-recovery procedure](../../operations/write-recovery.md) explains why its
marker pattern cannot establish the outcome of a multi-document Cosmos DB save.

See [query rules](../querying.md#rules-that-come-from-the-servers-store) and
[shared settings](shared.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
