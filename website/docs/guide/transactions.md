# Transactions

A client transaction is a real transaction on the server's database. `BeginTransaction` opens one
there and hands the client a token, and every request the client makes afterwards names that token,
so the server routes it to the same connection.

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

Order order = await context.Orders.SingleAsync(o => o.Id == 1);
order.CustomerId = "AROUT";
await context.SaveChangesAsync();

Customer customer = await context.Customers.SingleAsync(c => c.Id == "AROUT");
customer.Country = "Ireland";
await context.SaveChangesAsync();

await transaction.CommitAsync();
```

Six round trips, one transaction: the begin, two queries, two saves and the commit are each a
request, because each is an operation the server has to perform. If the second save fails,
`CommitAsync` is never reached and disposing the transaction rolls the first one back.

!!! warning "A transaction holds a server-side connection open"

    Between `BeginTransaction` and the commit, the server keeps a `DbContext` and its connection
    pinned to your token. Keep transactions short, and never let one span a user thinking about a
    dialog.

    **A client that vanishes mid-transaction pins all three until the server process exits.**
    There is no idle timeout and nothing reaps an abandoned token. `DisposeAsync` on the client
    covers every ordinary path including exceptions; it cannot cover a client that never runs
    again. Once such a transaction has written, it holds the store's write lock.

    **The token only resolves on the server instance that minted it.** The registry is
    process-local, so a load-balanced deployment needs session affinity for the life of a
    transaction.

## Savepoints

Where the server's database supports them, savepoints work as they do locally:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

await ApplyMandatoryChangesAsync(context);
await context.SaveChangesAsync();

await transaction.CreateSavepointAsync("mandatory_done");

try
{
    await ApplyOptionalChangesAsync(context);
    await context.SaveChangesAsync();
}
catch (DbUpdateException)
{
    await transaction.RollbackToSavepointAsync("mandatory_done");
}

await transaction.CommitAsync();
```

`RollbackToSavepointAsync` undoes the work after the savepoint and leaves the transaction open.
`ReleaseSavepointAsync` discards the savepoint and keeps the work.

A store that has no savepoints, such as EF Core's InMemory provider, reports that rather than
failing halfway. `transaction.SupportsSavepoints` answers before you try, and the answer comes from
the server's store rather than from a guess on the client.

## Two contexts, one transaction

Sometimes one screen needs several `DbContext` instances, such as a grid that refreshes on its own
beside a detail pane holding a unit of work, and the writes have to land together. A second context
joins the first one's transaction:

```csharp
await using var first = await factory.CreateDbContextAsync();
await using var second = await factory.CreateDbContextAsync();

await using IDbContextTransaction transaction = await first.Database.BeginTransactionAsync();

second.Database.UseInfoCarrierTransaction(transaction);   // (1)

await SaveTheFirstHalfAsync(first);
await SaveTheSecondHalfAsync(second);

await transaction.CommitAsync();                          // (2)
```

1. `UseInfoCarrierTransaction` is this provider's equivalent of the relational `UseTransaction`.
   There the shared thing is a `DbTransaction` on a connection; here it is the server's token,
   which is what makes sharing possible across a transport that may be stateless.
2. The joining context does not own the transaction. Ending it detaches `second` and leaves the
   transaction to whoever began it. Commit through the context that began it: two contexts able to
   commit the same transaction would make the outcome depend on disposal order.

`UseInfoCarrierTransaction` is an extension on `DatabaseFacade`, in the `InfoCarrier.Core`
namespace, and it throws `ArgumentException` if handed a transaction from another provider.

## Execution strategies and retries

The transaction lives on the server, and a retry has to re-run the whole unit of work rather than
just the failed request. Wrap the work the way you would with any provider:

```csharp
IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await context.Database.BeginTransactionAsync();
    await DoTheWorkAsync(context);
    await transaction.CommitAsync();
});
```

## What is not available

`IDbContextTransaction.GetDbTransaction()` and anything else that hands you a `DbTransaction` are
relational APIs, and there is no local connection to hand out. Ambient `TransactionScope` is
likewise not part of this provider's surface. The server's token is the whole of what can be shared,
which is what `UseInfoCarrierTransaction` takes.
