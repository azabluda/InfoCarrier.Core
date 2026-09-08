# Limitations

InfoCarrier.Core runs Microsoft's own Entity Framework Core specification suite, the same suite the
SQL Server, SQLite and InMemory providers run. This page lists every scenario in that suite which
does not behave the way a normal EF Core provider behaves, so you can judge whether any of them
affects your application.

It is complete for what a caller can observe as a limitation. The suite's other failures ask the
client for something only a database has, assert a refusal this provider does not need to make, or
are EF Core defects that every provider hits and this one reports with a different exception type.

```
Total tests: 29516, Passed: 29259, Failed: 19, Skipped: 238
```

Measured against `10.1.0`. The 238 skips are EF Core's own, tests EF itself skips for the
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
on the server from the values sent over the wire.

## Differences that are not limitations

These behave correctly, and differ from another EF Core provider only in ways you would notice
when porting code or tests.

### Exception message text for an untranslatable query

When a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does. The message text may differ. Two examples:

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

### Queries this provider answers that other providers do not

EF's suite has other scenarios that assert a provider either rejects the query or returns the wrong
rows. This provider answers them correctly. A test suite you port will expect an exception, and
LINQ that relies on this will not run unchanged elsewhere. Three of them:

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

Filtering a complex collection, then `Contains`:

```csharp
modelBuilder.Entity<RootEntity>()
    .ComplexCollection(e => e.AssociateCollection);   // stored by table splitting

var associates = LoadAssociates();
context.RootEntities
    .Where(e => e.AssociateCollection
        .Where(a => a.Id > associates[0].Id)
        .Contains(associates[1]))
    .ToList();
// EF Core providers: throws.   This provider: returns the matching rows.
```

Comparing a column collection against an inline collection of parameters, which relational
providers leave without a type mapping:

```csharp
var low = 1;
var high = 9;

context.Entities.Where(e => e.Ints == new[] { low, high }).ToList();
// EF Core providers: throws.   This provider: returns the matching rows.
```

A compiled query that puts a collection of parameters in a subquery behaves the same way. EF Core
has no type mapping to give it and refuses; this provider builds no SQL, so the question never
arises.

## Consequences of the client having no database

These are not defects. They follow from where the client sits.

| | |
|---|---|
| Relational-only APIs, such as `ExecuteSqlRaw`, `GetDbTransaction` and migrations, are not part of this provider's surface. Calling one throws. `FromSql` runs only where the server has granted it, and that grant is arbitrary SQL | [Querying](guide/querying.md#what-is-not-part-of-the-surface) |
| Automatic lazy loading does not work in Blazor WebAssembly | [Blazor WebAssembly](platforms/blazor-webassembly.md) |
| The client never sees the server's provider, so it assumes a relational store and refuses three queries relational providers refuse. `UseInfoCarrier(client, o => o.UseNonRelationalServerStore())` says otherwise | [Querying](guide/querying.md#rules-that-come-from-the-servers-store) |
| A query result arrives in one response rather than as a stream, so a very large result set is a very large response. Page it. | |
| Authentication and authorization are yours | [Security](security.md) |
| Native AOT is not supported: remoting a query means compiling an expression tree at runtime. Trimming is a separate question, and it works. | [Blazor WebAssembly](platforms/blazor-webassembly.md#trimming) |

## What this page cannot tell you

Every entry above corresponds to tests in EF Core's specification suite that run on every build.
The number of failing tests is gated in continuous integration, so it cannot grow without being
noticed. When an entry is fixed, or a new one appears, this page changes with it.

What the suite measures bounds what this page can promise. A conformance suite says nothing about
performance, payload size or concurrency under load. A scenario it never exercises is outside what
this page claims at the top.
