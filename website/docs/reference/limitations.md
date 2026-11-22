# Detailed limitations

Use this page to check a particular application requirement.
The ordinary [query](../applications/querying.md), [save](../applications/saving.md),
and [transaction](../applications/transactions.md) guides show the supported workflows.

## Check the operation you need

| Requirement | What to use |
|---|---|
| Create tables, run migrations, or access a database connection | The server's database provider; these operations are not available through the client context |
| Load relationships in a browser | `Include` or `LoadAsync`; synchronous browser HTTP and automatic lazy loading are unsupported |
| Share a transaction between client contexts | `UseInfoCarrierTransaction`; ambient `TransactionScope` and local `DbTransaction` access are unsupported |
| Retry after a connection failure | Application recovery logic; no retrying execution strategy is included |
| Publish with Native AOT | Unsupported; expression execution requires runtime code generation |
| Publish a trimmed browser client | Verify your application's published output; see [browser publishing](../applications/blazor-webassembly.md#publish-the-browser-client) |

## A client concurrency interceptor cannot suppress a server save failure

Affects you if an `ISaveChangesInterceptor` registered on the client returns
`InterceptionResult.Suppress()` from `ThrowingConcurrencyException` or
`ThrowingConcurrencyExceptionAsync`.

With EF Core, a suppression skips the conflicting row and the rest of the save goes ahead. Here the
server finds the conflict and rolls the save back before the client's interceptor runs.
`SaveChanges` returns 0 and does not throw. **Nothing in that save is written, and the change
tracker marks every change in it as saved.**

Register the interceptor on the server instead. The server runs EF Core's own save, so a
suppression there behaves as it does with EF Core: the other changes are written, and the client's
`SaveChanges` returns the count EF Core would.

```csharp
// On the server
builder.Services.AddDbContext<ShopContext>(o => o
    .UseSqlServer(connectionString)
    .AddInterceptors(new IgnoreConcurrencyConflicts()));
```

An interceptor on the client still sees the exception, so it can log it and let it throw.

## Differences that are not limitations

These behave correctly, and differ from another EF Core provider only in ways you would notice
when porting code or tests.

### Exception message text for an untranslatable query

When a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does. The message text may differ. Two examples:

```csharp
// (a) a method call where ExecuteUpdate expects a property
context.Orders
    .Where(o => o.Freight > 100m)
    .ExecuteUpdate(s => s.SetProperty(o => Math.Round(o.Freight), 0m));

// (b) a cast to a type no mapped entity implements
IQueryable orders = context.Orders;
orders.Cast<IArchivable>().FirstOrDefault();
```

Both throw. Catch the exception type and do not match on message text, which is unsupported on any
EF Core provider.

### Queries this provider answers that other providers do not

A local projection can answer a query that another EF Core provider refuses. For example, composing
LINQ over a collection stored through a value converter:

This fragment assumes your application declares Dashboard and Layout types, with a Layouts
collection stored through a value converter and matching comparison/conversion helpers:

```csharp
modelBuilder.Entity<Dashboard>()
    .Property(e => e.Layouts)
    .HasConversion(                       // List<Layout> stored as a single string column
        v => Serialize(v),
        v => Deserialize(v),
        layoutComparer);

context.Dashboards
    .Select(d => new { d.Name, Heights = d.Layouts.Select(l => l.Height).ToList() })
    .ToList();
// EF Core providers: throws.   This provider: reads the column and answers.
```

The inner lambda names `Layout`, which the model does not imply, so this client keeps that part of
the query and the server sends the whole column. Name `Layout` on both halves, as
[Keys of your own types](../applications/querying.md#keys-of-your-own-types) describes for a `GroupBy` key,
and the query reaches the server, which rejects it as every other provider does.

When moving that query to another provider, check whether it supports the converted collection
operation. Do not assume local result construction makes the same query portable.

Private or client-only helpers in a final projection can produce the same difference, such as
choosing a JSON collection element with a private index helper. Implicit local filtering, ordering,
grouping, joins and equality operations are refused. Call `AsEnumerable` before such an operator
to choose local execution explicitly.

For backend parity, use public shared helpers and register their types on both ends. The server's
provider then decides whether the query is supported. Registration never admits private methods.
See [Helpers of your own](../applications/querying.md#helpers-of-your-own).

## Consequences of the client having no database

These are not defects. They follow from where the client sits.

| | |
|---|---|
| Relational-only APIs, such as `ExecuteSqlRaw`, `GetDbTransaction` and migrations, are not part of this provider's surface. Calling one throws. `FromSql` runs only where the server has granted it, and that grant is arbitrary SQL | [Querying](../applications/querying.md#what-is-not-part-of-the-surface) |
| Automatic lazy loading does not work in Blazor WebAssembly | [Blazor WebAssembly](../applications/blazor-webassembly.md) |
| The client assumes relational collection projection rules. Set `InfoCarrierClientOptions.ServerUsesRelationalQueryRules` to `false` for another store. Implicit local row decisions remain refused | [Querying](../applications/querying.md#rules-that-come-from-the-servers-store) |
| The server's provider writes the SQL, so how a query is translated is settled there, not on the client. `EF.Constant` and `EF.Parameter` are part of your query, so they cross the wire and the server honours them | |
| `EF.CompileQuery` changes nothing about the wire. The server answers a compiled query once per execution, as it answers any other query | [Querying](../applications/querying.md#round-trips-and-result-size) |
| Asynchronous non-tracking queries stream rows. Tracked queries, single-result operations, and operators needing complete input remain buffered. Page large buffered results. | [Querying](../applications/streaming.md) |
| Authentication and authorization are yours | [Security](../operations/security.md) |
| Native AOT is not supported: remoting a query means compiling an expression tree at runtime. Trimming needs application-specific verification. | [Publishing a browser client](../applications/blazor-webassembly.md#publish-the-browser-client) |

## Check your application

Run representative queries and writes against your actual server provider.
Measure request sizes, response times, and behavior under the load your application expects.

Ambient transactions and local database connections are unsupported. There is no built-in retrying
execution strategy. See [transactions](../applications/transactions.md).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
