# Loading related data

Every way EF Core loads a navigation works here, and each one costs a round trip you can see.

## Eager loading, one round trip

`Include` is part of the query, so the related rows arrive with the principals:

```csharp
List<Order> orders = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Freight > 50m)
    .ToListAsync();

// orders[0].Customer is populated. No further request.
```

Filtered includes work too. This uses the tutorial's existing Customer and Order model:

```csharp
List<Customer> customers = await context.Customers
    .Include(c => c.Orders.Where(o => o.PlacedOn > cutoff))
    .ToListAsync();
```

Reach for this by default. One request that returns a graph beats several requests that return the
same graph in pieces.

## Explicit loading, one round trip when you ask

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

## Lazy loading, a round trip per touch

Lazy loading requires the Entity Framework Core proxies package, proxy-compatible entity classes,
and virtual navigation properties. The basic tutorial model does not enable proxies.
Apply these changes on both sides before using this example:

```csharp
// Client and server, both.
optionsBuilder.UseLazyLoadingProxies();
```

```csharp
Order order = await context.Orders.SingleAsync(o => o.Id == 1);

string company = order.Customer!.Company;   // fetches the customer now

```

Every touched navigation is a request, so a loop over orders can issue a separate request for each unloaded customer: the classic N+1, and much more expensive over a network than against a local database.
A navigation getter is also synchronous, so a lazy load blocks the calling thread on the round trip.
In a UI application that means loading off the UI thread, or accepting the freeze.

Enable proxies on both halves. The two models have to agree about everything the wire names, and
proxies add a model convention.

!!! danger "Not in Blazor WebAssembly"

    Automatic lazy loading is impossible in a browser: browser HTTP does not support the required synchronous request.
    Do not depend on when a platform exception occurs. Use
    `LoadAsync`. The whole story is on the
    [Blazor WebAssembly](../getting-started/blazor-webassembly.md) page.

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
entities. For a read-only grid this is usually the right shape. Combine it with `AsNoTracking`.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
