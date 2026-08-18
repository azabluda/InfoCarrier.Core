# Loading related data

Every way EF Core loads a navigation works here. The difference is that each one has a visible
price: a round trip.

## Eager loading: one round trip

`Include` is part of the query, so the related rows arrive with the principals:

```csharp
List<Order> orders = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Freight > 50m)
    .ToListAsync();

// orders[0].Customer is populated. No further request.
```

`ThenInclude` and filtered includes work the same way:

```csharp
List<Customer> customers = await context.Customers
    .Include(c => c.Orders.Where(o => o.PlacedOn > cutoff))
        .ThenInclude(o => o.Lines)   // an OrderLine collection on Order
    .ToListAsync();
```

**This is the one to reach for by default.** One request that returns a graph beats several
requests that return the same graph in pieces.

## Explicit loading: one round trip, when you ask

Load a navigation on an entity you already have:

```csharp
Customer customer = await context.Customers.SingleAsync(c => c.Id == "ALFKI");

await context.Entry(customer).Collection(c => c.Orders).LoadAsync();
await context.Entry(order).Reference(o => o.Customer).LoadAsync();
```

Each `LoadAsync` is exactly one request. Use it for a master-detail screen, where the detail is
fetched when a row is selected rather than for every row in the list.

You can filter or aggregate before loading, which sends the query rather than the whole collection:

```csharp
int count = await context.Entry(customer)
    .Collection(c => c.Orders)
    .Query()
    .CountAsync(o => o.Freight > 50m);
```

## Lazy loading: a round trip per touch

Lazy loading works, through EF Core's proxies package as usual:

```csharp
// Client and server, both.
optionsBuilder.UseLazyLoadingProxies();
```

```csharp
Order order = await context.Orders.SingleAsync(o => o.Id == 1);

string company = order.Customer!.Company;   // fetches the customer now
int lines = order.Lines.Count;              // and its lines now
```

Two things to weigh before enabling it:

- Every touched navigation is a request. A loop over 100 orders that reads `order.Customer`
  makes 100 requests. This is the classic N+1, and over a network it is much more expensive than
  it is against a local database.
- A navigation getter is synchronous, so a lazy load blocks the calling thread on the round
  trip. In a UI application that means loading off the UI thread or accepting the freeze.

If you enable proxies, enable them on **both** halves. The two models have to agree about
everything the wire names, and proxies add a model convention.

!!! danger "Not in Blazor WebAssembly"

    Automatic lazy loading is impossible in a browser: WebAssembly is single-threaded and cannot
    block, so the synchronous getter throws *after* the request has already gone out. Use
    `LoadAsync`. The whole story is on the
    [Blazor WebAssembly](../platforms/blazor-webassembly.md) page.

## Choosing

| You want | Use | Cost |
|---|---|---|
| A list plus its related data | `Include` | one request |
| Detail for the row the user just clicked | `LoadAsync` | one request, when clicked |
| A count or a filtered subset of a navigation | `.Collection(…).Query()` | one request, small answer |
| Convenience in a non-UI, non-browser client | lazy loading | one request per touched navigation |

## Only what you need

A projection sends less than an `Include`, because only the selected columns cross the wire:

```csharp
var summary = await context.Orders
    .Where(o => o.PlacedOn > cutoff)
    .Select(o => new { o.Id, o.PlacedOn, Company = o.Customer!.Company })
    .ToListAsync();
```

The join happens on the server, and what comes back is three values per row rather than two whole
entities. For a read-only grid this is usually the right shape; combine it with `AsNoTracking`.
