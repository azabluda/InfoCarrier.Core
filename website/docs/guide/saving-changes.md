# Saving changes

`SaveChanges` is a unit of work, and on a remote provider that is more than a figure of speech.
Everything the change tracker holds travels in one request, is replayed against a real `DbContext`
on the server, and is written in one `SaveChanges` there.

```csharp
Order order = await context.Orders.SingleAsync(o => o.Id == 2);
order.Freight = 15m;

context.Orders.Add(new Order
{
    CustomerId = "AROUT",
    PlacedOn = DateTime.UtcNow,
    Freight = 5m,
});

int written = await context.SaveChangesAsync();   // one round trip, returns 2
```

One edit and one insert, one request. Batch your work into a unit rather than saving after every
change.

## Store-generated values come back

The client cannot know the key an identity column will produce, so before the save an added
entity's key is a placeholder. After the save it is the real value:

```csharp
var order = new Order { CustomerId = "AROUT", PlacedOn = DateTime.UtcNow, Freight = 5m };
context.Orders.Add(order);

await context.SaveChangesAsync();

Console.WriteLine(order.Id);   // the key the server's database issued
```

The same applies to any store-generated property: computed columns, default values, concurrency
tokens.

!!! note "Before the save, read the key from the entry"

    Right after `Add`, `order.Id` is still `0`: EF holds the temporary value on the *entry*, not on
    your instance. If you need it before saving, ask for it explicitly:

    ```csharp
    var temporary = context.Entry(order).Property(o => o.Id).CurrentValue;
    ```

## Graphs

Add a whole object graph and it is saved in dependency order, with the foreign keys filled in from
the keys the store issues:

```csharp
var customer = new Customer
{
    Id = "NEWCO",
    Company = "New Company",
    Country = "Norway",
    Orders =
    [
        new Order { PlacedOn = DateTime.UtcNow, Freight = 12m },
        new Order { PlacedOn = DateTime.UtcNow, Freight = 30m },
    ],
};

context.Customers.Add(customer);
await context.SaveChangesAsync();
```

Many-to-many relationships, including their join rows, work the same way.

## Deleting

```csharp
Order order = await context.Orders.SingleAsync(o => o.Id == 4);
context.Orders.Remove(order);
await context.SaveChangesAsync();
```

To delete without loading the row first, use `ExecuteDeleteAsync`. See
[Querying](querying.md#bulk-operations).

## Concurrency

Optimistic concurrency behaves as it does on any EF Core provider. A concurrency token that no
longer matches makes the server's save fail, and the failure arrives on the client as
`DbUpdateConcurrencyException`:

```csharp
try
{
    await context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    foreach (EntityEntry entry in ex.Entries)
    {
        PropertyValues? current = await entry.GetDatabaseValuesAsync();   // a round trip
        // resolve, then retry
    }
}
```

`GetDatabaseValuesAsync` reads through the server like any other query, so it costs a request.

## What the client sends

Only the entries the change tracker considers changed, and only what the server needs to replay
them: which entity type, which key, which properties changed, and their values. Entities you merely
queried and did not touch stay on the client.

The server's context is not your context. It replays your changes against a fresh `DbContext` on
its own model, then discards it. Server-side query filters, interceptors and `SaveChanges` overrides
are applied there, which is also what stops a client from writing anything the server's model does
not expose.

## Errors

A failure on the server, such as a constraint violation or a validation exception in an
interceptor, arrives on the client as the exception EF would have thrown locally, with its message
and inner chain preserved. See [Handling errors](errors.md).
