# Query data

Use the context from the [first tutorial](../getting-started/first-app.md).
Write ordinary LINQ and execute the query with an Entity Framework Core method:

```csharp
List<Customer> customers = await context.Customers
    .Where(c => c.Country == "Germany")
    .OrderBy(c => c.Company)
    .ToListAsync();

foreach (Customer customer in customers)
{
    Console.WriteLine(customer.Company);
}
```

The server filters and orders the rows before returning them.
Against the tutorial's seeded database, this finds customer `ALFKI`.

## Filter and page a list

Build an `IQueryable` before executing it. This lets the server apply filters and paging together:

```csharp
string? country = "Germany";
int pageIndex = 0;
int pageSize = 20;

IQueryable<Customer> query = context.Customers;
if (!string.IsNullOrEmpty(country))
{
    query = query.Where(c => c.Country == country);
}

List<Customer> page = await query
    .OrderBy(c => c.Company).ThenBy(c => c.Id)
    .Skip(pageIndex * pageSize)
    .Take(pageSize)
    .ToListAsync();

int matching = await query.CountAsync();
```

The page query returns at most 20 rows. The count is a separate request.
Use a stable order before paging. The key resolves ties between equal company names.
Do not load a list and then page it locally when the server can do that work.

## Select the values you need

For a read-only list, select display values instead of loading complete entities and relationships:

```csharp
var summary = await context.Orders
    .OrderBy(o => o.Id)
    .Select(o => new { o.Id, o.PlacedOn, Company = o.Customer!.Company })
    .Take(20)
    .ToListAsync();
```

The server performs the relationship lookup. The result contains the selected values.
Use [related-data loading](related-data.md) when you need editable entity graphs.

## Tracking and saving

Ordinary entity queries track the returned entities. Change a property and call
[SaveChangesAsync](saving.md) to save it.

For read-only entity results, add `AsNoTracking()`.
This avoids keeping change-tracking state for data you will only display.
Use a fresh context for each independent operation.

## Stream a result

Use [streaming](streaming.md) when the application can process rows as they arrive.
Tracked queries and some operations buffer their result instead.

## When a query contains application code

Most queries use the database provider's supported operators.
If a query calls your own helper or constructs a custom result, execution location matters.
The following sections explain those cases. They are not prerequisites for the queries above.

## What runs where

The client compiles your query, works out how much of it the server can run, and sends that much.
There are three cases, and the projection usually tells you which one you are in.

### The whole query goes

If the server can execute everything in the query, the entire tree is sent and the server answers
with rows or a scalar. An aggregate returns a number, not the rows behind it.

```csharp
var byCustomer = await context.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(o => o.Freight) })
    .ToListAsync();
```

### The query is split

If the projection contains code the server cannot run, such as one of your own methods, the query is
cut at that point: the server runs the part that reaches the data, and the client runs the
projection over what comes back.

```csharp
public static class Formatting
{
    public static string Describe(decimal freight) => $"EUR {freight:0.00}";
}

var report = await context.Orders
    .Where(o => o.Freight > 0m)                                          // (1) server
    .Select(o => new { o.Id, Label = Formatting.Describe(o.Freight) })   // (2) client
    .ToListAsync();
```

1. The filter is part of the tree the server executes, so only matching rows cross the wire.
2. `Formatting.Describe` runs in your client process, over the rows that arrived.

The filter is not dragged back to the client just because the projection cannot be translated, and
an unfiltered query with a client-side projection therefore fetches everything. Put the `Where` in
before you worry about the `Select`. `Formatting.Describe` has to be a static method on a type,
because a local function will not compile inside a query.

### The query cannot be translated

An expression sent to the server follows its provider's translation rules, including refusal.
Client-only projections can differ, as [Helpers of your own](#helpers-of-your-own) explains.
Catch the exception type rather than matching message text, which is not a supported contract.
See [Limitations](../reference/limitations.md).

### Helpers of your own

To let the server provider handle your helper, put it on a shared type and make the method public.
Add its type to your shared deployment configuration, then call that method on both ends:

```csharp
services.AddInfoCarrierServer<ShopContext>(o => o.ConfigureShared(ShopDeployment.ConfigureSharedContract));
services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureSharedContract));

public static class ShopDeployment
{
    public static void ConfigureSharedContract(InfoCarrierDeploymentBuilder builder)
        => builder.AddAllowedTypes(typeof(Formatting));
}
```

The helper must exist on the server too. Registration lets the expression reach that server;
its provider decides whether to translate it, evaluate a final projection, or refuse it.
Registration does not make a method translatable or admit private methods. Register only trusted
types, as [Security](../operations/security.md) describes.

A private or genuinely client-only helper can remain in the local projection. Such a query may
answer where plain EF Core refuses the projection. Operators that decide rows, order, groups,
joins or equality must reach the server, or follow an explicit `AsEnumerable` boundary.
Do not rely on those answers when porting queries to another provider.

### Rules that come from the server's store

The client assumes the store is relational when validating collection projections. It refuses
`Distinct`, `Union`, `Concat`, `Except` or `Intersect` over a projection that loses the columns
needed to identify collection rows. For another store, disable these relational checks:

```csharp
services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .WithServerUsesRelationalQueryRules(false));
```

The backend still decides whether the query works. This setting does not permit implicit local
filtering, ordering, grouping, joins or equality operations.

## What is not part of the surface

Relational-only APIs are not part of this provider: `Database.ExecuteSqlRaw`, `GetDbTransaction`,
migrations and `EnsureCreated`. Schema management belongs on the server, where the real provider is.
Expose it as a server-side operation of your own.

The model is a different matter. The client builds EF Core's relational model, so `GetTableName()`
and `Model.GetRelationalModel()` answer, and tooling that reads them works against a client context.
They read your own `[Table]` attributes and `DbSet` names, which both halves compile, so the two
models agree. Nothing the client computes from them reaches the server.

`FromSql` and `Database.SqlQuery<T>` work, but only where both roles opt in. Call
`AllowArbitrarySqlExecution(true)` in both role callbacks. Without both settings, the query is
refused like any untranslatable one.

Grant it with care. One command text runs every statement in it, and an uncomposed `FromSql` reaches
the database unchanged, so a caller who has the grant can run any SQL the database allows, with the
server's own rights. The server's query filters are not in such a query.

`EF.Functions.Like`, `EF.Constant` and `EF.Parameter` cross the wire, and so do the functions you
map yourself with `HasDbFunction`. A store's own family, such as `SqliteDbFunctionsExtensions`, is
a type this package cannot name, so add it through `AddAllowedTypes(...)` in your shared method.

## Keys of your own types

`GroupBy`, `Join`, `GroupJoin` and `DistinctBy` each take a key. Register named key types on both
client and server. InfoCarrier rejects implicit local filtering, ordering, grouping, joins,
distinctness and equality before reading database rows. Final client projections remain supported.

```csharp
public readonly record struct PeriodKey(int Year, int Month);
```

After registering that type on both sides, a query can use it as a key:

```csharp
IQueryable<Order> orders = context.Orders;
var query = orders.GroupBy(o => new PeriodKey(o.PlacedOn.Year, o.PlacedOn.Month));
```

Use the same registration method for both roles:

```csharp
services.AddInfoCarrierServer<ShopContext>(o => o.ConfigureShared(ShopDeployment.ConfigureSharedContract));
services.AddInfoCarrierClient(o => o
    .WithEndpoint("https://api.example.com/infocarrier")
    .ConfigureShared(ShopDeployment.ConfigureSharedContract));

public static class ShopDeployment
{
    public static void ConfigureSharedContract(InfoCarrierDeploymentBuilder builder)
        => builder.AddAllowedTypes(typeof(PeriodKey));
}
```

Safe internal rewrites, such as anonymous composite join keys carried as tuples, keep operators
on the server. Other transported anonymous structures need `AddAnonymousShapes` on both roles.

Registration permits transport. The backend still decides translation, results or refusal;
InfoCarrier never retries a server refusal through local evaluation. To choose local execution,
call `AsEnumerable` before the operator. For example, `orders.AsEnumerable().GroupBy(...)` groups
locally and transfers every selected order. Apply server filters and select needed columns first.

## Round trips and result size

Every materialized query is a request, so a loop that queries per item makes one request per item.
Compose the query instead, or fetch what you need with `Include`. To count what a screen actually
costs, see [Counting round trips](../operations/diagnostics.md#counting-round-trips).

Requests towards the server have a default size limit; answers coming back have none, because the
library has no basis for capping how large an answer your own query may have. To set one, see
[client configuration](../reference/options.md#byte-limits).

For updates or deletes over a filtered set, use [bulk operations](saving.md#bulk-operations).

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
