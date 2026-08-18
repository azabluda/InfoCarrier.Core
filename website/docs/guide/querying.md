# Querying

You write EF Core LINQ. There is no InfoCarrier query API.

```csharp
List<Order> recent = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Customer!.Country == "Germany" && o.Freight > 50m)
    .OrderByDescending(o => o.PlacedOn)
    .Take(20)
    .ToListAsync();
```

One round trip. The `Where`, the `OrderByDescending` and the `Take` all execute on the server,
against the server's provider, and 20 rows come back. Nothing is filtered in the client.

## What runs where

The client compiles your query, works out how much of it the server can run, and sends that much.
Three cases, and you can usually tell which one you are in by looking at the projection.

### The whole query goes

If everything in the query is something the server can execute, such as properties of mapped
entities, operators and the LINQ methods EF translates, the entire tree is sent and the server
answers with
rows or a scalar.

```csharp
decimal total = await context.Orders.SumAsync(o => o.Freight);
int germans = await context.Customers.CountAsync(c => c.Country == "Germany");

var byCustomer = await context.Orders
    .GroupBy(o => o.CustomerId)
    .Select(g => new { CustomerId = g.Key, Count = g.Count(), Total = g.Sum(o => o.Freight) })
    .ToListAsync();
```

An aggregate returns a number, not the rows behind it.

### The query is split

If the projection contains code the server cannot run, such as one of your own methods, a locally
defined type or formatting, the query is cut at that point. The server runs the part that reaches the
data; the client runs the projection over what comes back.

```csharp
public static class Formatting
{
    public static string Describe(decimal freight) => $"EUR {freight:0.00}";
}

var report = await context.Orders
    .Where(o => o.Freight > 0m)                              // (1) server
    .Select(o => new { o.Id, Label = Formatting.Describe(o.Freight) })   // (2) client
    .ToListAsync();
```

1. The filter is part of the tree the server executes. Only matching rows cross the wire.
2. `Formatting.Describe` runs in your client process, over the rows that arrived.

This is the behaviour you want: the filter is not dragged back to the client just because the
projection cannot be translated. It also means an unfiltered query with a client-side projection
fetches everything, so put the `Where` in before you worry about the `Select`.

!!! warning "A local function cannot appear in a query"

    ```csharp
    string Describe(decimal f) => $"EUR {f:0.00}";      // local function
    .Select(o => new { o.Id, Label = Describe(o.Freight) })   // will not compile
    ```

    `An expression tree may not contain a reference to a local function` is a C# rule, not this
    provider's. Make it a static method on a type, as above.

### The query cannot be translated

Where EF Core itself would refuse a query, this provider refuses it too, with an
`InvalidOperationException`, the same exception type EF throws. **Catch the type, never match on
the message**: message text is not a supported contract on any EF Core provider, and a couple of
messages here differ from other providers' by wording. See [Limitations](../limitations.md).

## Tracking

Change tracking works exactly as it does with any provider: the identity map, navigation fix-up
and all. For a read-only screen, opt out:

```csharp
List<Customer> rows = await context.Customers
    .AsNoTracking()
    .OrderBy(c => c.Id)
    .ToListAsync();
```

`AsNoTracking` is worth more here than in a local application: it skips building change-tracking
state for rows you are only going to display, and it keeps a long-lived client context from
accumulating entities it will never write.

## Paging

Compose `Skip`/`Take` on the `IQueryable`, before materializing:

```csharp
IQueryable<Customer> query = context.Customers;

if (!string.IsNullOrEmpty(country))
{
    query = query.Where(c => c.Country == country);
}

List<Customer> page = await query
    .OrderBy(c => c.Company)          // (1)
    .Skip(pageIndex * pageSize)
    .Take(pageSize)
    .ToListAsync();

int matching = await query.CountAsync();
```

1. **Order before you page, and order on the entity.** If you sort a client-side projection
   instead, the server pages an unordered set and the client sorts the page, giving one page of the
   wrong
   rows, in the right order. The sample's Customers grid is built this way for exactly this reason.

## Bulk operations

`ExecuteUpdate` and `ExecuteDelete` are supported. They run on the server and never load the rows:

```csharp
int updated = await context.Orders
    .Where(o => o.CustomerId == "AROUT")
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Freight, o => o.Freight + 1m));

int deleted = await context.Orders
    .Where(o => o.PlacedOn < cutoff)
    .ExecuteDeleteAsync();
```

Both return the number of rows affected. As with any EF Core provider, neither updates your local
change tracker, so the entities the client already holds keep their old values until reloaded.

## What is not part of the surface

The client has no database and no relational provider, so relational-only APIs (`FromSql`,
`Database.ExecuteSqlRaw`, `GetDbTransaction`, migrations, `EnsureCreated`) are not what this
provider offers. Schema management belongs on the server, where the real provider is, and so does
any raw SQL: expose it as a server-side operation of your own rather than sending SQL from a
client.

## Two things to keep in mind

**Every materialized query is a network round trip.** `ToListAsync`, `FirstOrDefaultAsync`,
`CountAsync`, and each is a request. A loop that queries per item is a loop that makes one request per
item. Compose the query instead, or fetch what you need with `Include`.

**Result size is your query's business.** There is a default size bound on requests *towards* the
server and none on the answer coming back, because the library has no basis for capping how large
an answer your own query may have. If you want one, see
[client configuration](../configuration/client.md#payload-limits).
