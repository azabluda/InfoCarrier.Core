# Recover an uncertain write

A connection failure can happen after the database commits but before the client receives the answer.
Sending the write again can create duplicate data.
The same uncertainty applies to cancellation after sending a write.

This procedure uses a marker saved in the same atomic operation as the business changes.
It follows Entity Framework Core's
[transaction-marker pattern](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency#option-4---manually-track-the-transaction).

## Add a durable marker

Extend the shared model with:

```csharp
public class SaveMarker
{
    public Guid Id { get; set; }
}

// Add to ShopContext:
public DbSet<SaveMarker> SaveMarkers => Set<SaveMarker>();
```

Create the corresponding table on the server.
The marker's primary key must be unique.
Generate its identifier before sending the save and keep it in durable client storage.
You need that same identifier after an application restart.

## Save and verify

Add the marker to the context holding your business changes:

```csharp
Guid operationId = Guid.NewGuid();
// Persist operationId before sending the request.
context.SaveMarkers.Add(new SaveMarker { Id = operationId });
context.Orders.Add(order);
await context.SaveChangesAsync();
```

If the response is lost, stop using that context for recovery.
Create a fresh context and check the server:

```csharp
await using var verification = await factory.CreateDbContextAsync();
bool committed = await verification.SaveMarkers.AsNoTracking()
    .AnyAsync(m => m.Id == operationId);
```

| Result | What to do |
|---|---|
| Marker found | The atomic save committed. Reload affected rows to recover generated keys and values. |
| Marker absent | Do not assume an earlier request has stopped. If retrying, rebuild the same logical operation with the same marker identifier in a new context. |
| Verification fails | The outcome is still unknown. Keep the identifier and retry verification later. |

If two attempts race, the marker's unique key prevents both atomic saves from committing.
A duplicate-key response still needs verification; do not treat every database error as success.
Limit retries, preserve the operation identifier, and make recovery visible to the user.
Delete old markers according to your application's retention policy.

## Conditions this requires

The marker and business changes must commit or roll back together.
This applies to a transactional relational save. Check the document provider's transaction behavior;
do not infer it from this library's request boundary.
The MongoDB comparison tier is temporarily disabled during the EF Core 11 migration.
Its earlier results do not establish transaction behavior for an EF Core 11 provider.

EF Core 11's Cosmos DB provider groups writes into transactional batches by container and partition.
The default can use several batches, so a marker in one batch does not prove another batch committed.
Use the marker pattern only when the marker and all business changes are guaranteed to share one transaction.
See Microsoft's [Cosmos DB transaction guidance](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/whatsnew#transactional-batches).

Generated values in a lost response never reached the client.
Prefer application-generated identifiers where appropriate, or reload using a durable business key.
Calling `AcceptAllChanges` alone cannot recover missing generated values.

## Several saves or bulk updates

For work inside an explicit transaction, save the marker inside that transaction too.
This includes `ExecuteUpdate` and `ExecuteDelete`.
After an uncertain commit, check the marker through a fresh context outside that transaction.
Do not retry a commit on the uncertain token.

InfoCarrier does not provide automatic duplicate-write prevention or a retrying execution strategy.
See [transaction deployment](transactions.md) and [error handling](errors.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
