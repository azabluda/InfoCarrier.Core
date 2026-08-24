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

One round trip. The `Where`, the `OrderByDescending` and the `Take` all execute on the server, and
20 rows come back. Nothing is filtered in the client.

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

Where EF Core itself would refuse a query, this provider refuses it too, with the same
`InvalidOperationException`. Catch the type and never match on the message: message text is not a
supported contract on any EF Core provider, and a couple of messages here are worded differently
from other providers'. See [Limitations](../limitations.md).

## Tracking

Change tracking works as it does with any provider, identity map and navigation fix-up included.
For a read-only screen, opt out with `AsNoTracking()`. It is worth more here than in a local
application: it skips change-tracking state for rows you are only going to display, and it keeps a
long-lived client context from accumulating entities it will never write.

## Paging

Compose `Skip` and `Take` before materializing:

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

1. Order before you page, and order on the entity. Sort a client-side projection instead and the
   server pages an unordered set while the client sorts the page: one page of the wrong rows, in
   the right order.

## Bulk operations

`ExecuteUpdate` and `ExecuteDelete` run on the server and never load the rows.

```csharp
int updated = await context.Orders
    .Where(o => o.CustomerId == "AROUT")
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Freight, o => o.Freight + 1m));

int deleted = await context.Orders
    .Where(o => o.PlacedOn < cutoff)
    .ExecuteDeleteAsync();
```

Both return the number of rows affected and, as with any EF Core provider, neither updates your
local change tracker.

## What is not part of the surface

Relational-only APIs are not part of this provider: `FromSql`, `Database.ExecuteSqlRaw`,
`GetDbTransaction`, migrations and `EnsureCreated`. Schema management and raw SQL belong on the
server, where the real provider is. Expose them as server-side operations of your own.

## Round trips and result size

Every materialized query is a request, so a loop that queries per item makes one request per item.
Compose the query instead, or fetch what you need with `Include`.

Requests towards the server have a default size limit; answers coming back have none, because the
library has no basis for capping how large an answer your own query may have. To set one, see
[client configuration](../configuration/client.md#payload-limits).
