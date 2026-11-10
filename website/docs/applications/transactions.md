# Use transactions

Use the Shop model and seeded rows from the [first tutorial](../getting-started/first-app.md).
A single save is already a unit of work. Add an explicit transaction when several requests must commit together.

Begin a transaction before the related requests and commit after they all succeed.
The server keeps their database work in one transaction:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

Order order = await context.Orders.SingleAsync(o => o.Id == 1);
order.Freight = 10m;
await context.SaveChangesAsync();

Customer customer = await context.Customers.SingleAsync(c => c.Id == "ALFKI");
customer.Country = "Ireland";
await context.SaveChangesAsync();

await transaction.CommitAsync();
```

Six round trips, one transaction: the begin, two queries, two saves and the commit are each a
request, because each is an operation the server has to perform. If the second save fails,
`CommitAsync` is never reached and disposing the transaction rolls the first one back.

A transaction holds a server context and database connection between requests.
Configure [routing, expiry, and caller identity](../operations/transactions.md) before deployment.

## Savepoints

Where the server's database supports them, savepoints work as they do locally:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync();

Customer customer = await context.Customers.SingleAsync(c => c.Id == "ALFKI");
customer.Company = "Confirmed Shop";
await context.SaveChangesAsync();

await transaction.CreateSavepointAsync("mandatory_done");

var optionalOrder = new Order
{
    CustomerId = "ALFKI", PlacedOn = DateTime.UtcNow, Freight = 5m
};

try
{
    context.Orders.Add(optionalOrder);
    await context.SaveChangesAsync();
}
catch (DbUpdateException)
{
    await transaction.RollbackToSavepointAsync("mandatory_done");
    context.Entry(optionalOrder).State = EntityState.Detached;
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

var customer = await first.Customers.SingleAsync(c => c.Id == "ALFKI");
customer.Company = "Updated together";
await first.SaveChangesAsync();

var order = await second.Orders.SingleAsync(o => o.Id == 1);
order.Freight = 10m;
await second.SaveChangesAsync();

await transaction.CommitAsync();                          // (2)
```

1. `UseInfoCarrierTransaction` joins the transaction that `first` began.
2. The joining context does not own the transaction. Ending it detaches `second` and leaves the
   transaction to whoever began it. Commit through the context that began it: two contexts able to
   commit the same transaction would make the outcome depend on disposal order.

`UseInfoCarrierTransaction` is an extension on `DatabaseFacade`, in the `InfoCarrier.Core`
namespace. Import `Microsoft.EntityFrameworkCore.Storage` for `IDbContextTransaction`.
It throws `ArgumentException` if handed a transaction from another provider.

## Execution strategies and retries

The transaction lives on the server, and a retry has to re-run the whole unit of work rather than
just the failed request. The following is a pattern for applications that supply a retrying strategy;
`DoTheWorkAsync` stands for the complete application operation:

```csharp
IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await context.Database.BeginTransactionAsync();
    await DoTheWorkAsync(context);
    await transaction.CommitAsync();
});
```

This provider ships no retrying strategy. `CreateExecutionStrategy()` returns EF Core's default,
which runs your work once and gives up on the first failure. Only add a retrying strategy when the complete operation can safely be repeated.

To supply one, replace `IExecutionStrategyFactory` in EF's internal service provider. The
`optionsBuilder.ExecutionStrategy(...)` overload you may know from SQL Server is a relational
option, and a client's options are not relational. See
[the internal service provider](../extending/services.md#entity-framework-provider-services).

A retry re-runs everything inside the block, the `BeginTransaction` included, so the work has to be
repeatable. Read [Handling errors](../operations/errors.md) first: a request that failed in transport leaves the
outcome unknown, so retrying a `SaveChanges` can write the same row twice.

## What is not available

`IDbContextTransaction.GetDbTransaction()` and anything else that hands you a `DbTransaction` are
relational APIs, and there is no local connection to hand out. Ambient `TransactionScope` is
likewise not part of this provider's surface. The server's token is the whole of what can be shared,
which is what `UseInfoCarrierTransaction` takes.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
