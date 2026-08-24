# Limitations

InfoCarrier.Core runs Microsoft's own Entity Framework Core specification suite, the same suite the
SQL Server, SQLite and InMemory providers run. This page lists every scenario in that suite which
does not behave the way a normal EF Core provider behaves, so you can judge whether any of them
affects your application.

It is complete for what the suite covers: if the suite has a scenario and it is not on this page,
it passes.

```
Total tests: 22662, Passed: 22476, Failed: 9, Skipped: 177
```

Measured against `10.0.0-preview.1`. The 177 skips are EF Core's own, tests EF itself skips for the
store behind them, not suppressions added here.

## Not supported

### Inserting an entity whose complex property is a property bag

Affects you if you map a complex property, or a complex collection, whose CLR type is
`Dictionary<string, object>`. EF Core calls this a property bag: the shape is declared in the model
rather than in the CLR type.

```csharp
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";

    // A property bag: no CLR properties, the members come from the model below.
    public Dictionary<string, object> Spec { get; set; } = new();
}

modelBuilder.Entity<Product>()
    .ComplexProperty(e => e.Spec, "Spec", b =>
    {
        b.Property<string>("Material");
        b.Property<double>("WeightKg");
    });
```

The insert throws:

```csharp
context.Products.Add(new Product
{
    Sku = "BOLT-M6",
    Spec = { ["Material"] = "steel", ["WeightKg"] = 0.012 },
});

await context.SaveChangesAsync();   // throws
```

The same applies to the collection form, `List<Dictionary<string, object>>` mapped with
`ComplexCollection`.

Querying and change tracking work. Inserting throws. EF's suite does not cover updating or deleting
for this shape, so treat the whole write path as unsupported rather than assuming update works.

The workaround is to declare the complex type as an ordinary class:

```csharp
public class ProductSpec
{
    public string Material { get; set; } = "";
    public double WeightKg { get; set; }
}

modelBuilder.Entity<Product>().ComplexProperty(e => e.Spec);
```

Nested complex types and complex collections are fine, as long as the type is a class rather than a
dictionary.

The cause is a defect in EF Core's own materializer, reached because this provider rebuilds entities
on the server from the values sent over the wire. Tracked upstream as
[dotnet/efcore#36175](https://github.com/dotnet/efcore/issues/36175).

## Use with caution

### Three-level correlated collections with `Distinct`

Affects you if you project nested collections three levels deep and apply `Distinct` to the
innermost one.

```csharp
var report = context.Customers
    .Select(c => new
    {
        c.Name,
        Orders = c.Orders.Select(o => new
        {
            o.PlacedOn,
            Products = o.Lines.Select(l => l.ProductName).Distinct().ToList(),
        }).ToList(),
    })
    .ToList();
```

The query runs and returns a result. No EF Core provider supports this query: SQL Server, SQLite
and InMemory all reject it. Because no provider executes it, there is no reference answer to check
this one against, so the result returned here is unverified.

Apply `Distinct` after materializing instead:

```csharp
Products = o.Lines.Select(l => l.ProductName).ToList(),   // then .Distinct() in memory
```

## Differences that are not limitations

These behave correctly, and differ from another EF Core provider only in ways you would notice
when porting code or tests.

### Exception message text for an untranslatable query

When a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does. The message text may differ in two cases: a method call inside an `ExecuteUpdate`
property selector, and a cast to a type nothing in your model implements.

```csharp
// (a) a method call where ExecuteUpdate expects a property
context.Orders
    .Where(o => o.Total > 100m)
    .ExecuteUpdate(s => s.SetProperty(o => Math.Round(o.Total), 0m));

// (b) a cast to a type no mapped entity implements
IQueryable orders = context.Orders;
orders.Cast<IArchivable>().FirstOrDefault();
```

Both throw. Catch the exception type and do not match on message text, which is unsupported on any
EF Core provider.

### Queries this provider answers that other providers reject

Two scenarios in EF's suite assert that a provider rejects the query, and this provider answers
them correctly instead. A test suite you port from another provider will expect an exception here,
and LINQ that relies on this leniency will not run unchanged on a relational provider.

Composing LINQ over a collection stored through a value converter:

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
// EF Core providers: throws.   This provider: returns the rows.
```

`Contains` over a collection of enums stored as a string:

```csharp
modelBuilder.Entity<User>()
    .Property(e => e.Roles)               // List<Role> stored as "Seller,Buyer"
    .HasConversion(
        v => string.Join(',', v),
        v => ParseRoles(v),
        roleComparer);

var role = Role.Seller;
context.Users.Where(u => u.Roles.Contains(role)).ToList();
// EF Core providers: throws.   This provider: returns the matching rows.
```

## Consequences of the client having no database

These are not defects. They follow from where the client sits.

| | |
|---|---|
| Relational-only APIs, such as `FromSql`, `ExecuteSqlRaw`, `GetDbTransaction` and migrations, are not part of this provider's surface | [Querying](guide/querying.md#what-is-not-part-of-the-surface) |
| Automatic lazy loading does not work in Blazor WebAssembly | [Blazor WebAssembly](platforms/blazor-webassembly.md) |
| A query result arrives in one response rather than as a stream, so a very large result set is a very large response. Page it. | |
| Authentication and authorization are yours | [Security](security.md) |
| Native AOT is not supported: remoting a query means compiling an expression tree at runtime. Trimming is a separate question, and it works. | [Blazor WebAssembly](platforms/blazor-webassembly.md#trimming) |

## What this page cannot tell you

Every entry above corresponds to tests in EF Core's specification suite that run on every build.
The number of failing tests is gated in continuous integration, so it cannot grow without being
noticed. When an entry is fixed, or a new one appears, this page changes with it.

What the suite measures bounds what this page can promise. A conformance suite says nothing about
performance, payload size or concurrency under load, and nothing about the relational APIs this
provider does not have. A scenario it never exercises is outside what this page claims at the top.
