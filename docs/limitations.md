# Known limitations

InfoCarrier.Core runs Microsoft's own Entity Framework Core specification test suite — the same
suite EF Core's SQL Server, SQLite and InMemory providers run. This page lists **every scenario in
that suite that does not behave the way a normal EF Core provider behaves**, so you can judge
whether any of them affects your application.

It is a complete list, not a selection. If a scenario is not on this page, it is covered by the
suite and it passes.

---

## Not supported

### Inserting an entity whose complex property is a property bag

**Affects you if** you map a complex property — or a complex collection — whose CLR type is
`Dictionary<string, object>`. EF Core calls this a *property bag*: the shape is declared in the
model rather than in the CLR type.

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

**What happens** — the insert throws:

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

**Scope.** Querying and change tracking work. Inserting throws. **Updating and deleting are not
covered by EF's suite for this shape**, so treat the whole write path as unsupported rather than
assuming update works.

**Workaround** — declare the complex type as an ordinary class:

```csharp
public class ProductSpec
{
    public string Material { get; set; } = "";
    public double WeightKg { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public ProductSpec Spec { get; set; } = new();
}

modelBuilder.Entity<Product>().ComplexProperty(e => e.Spec);
```

Every other complex-type shape is supported, including nested complex types and complex
collections, as long as the type is a class rather than a dictionary.

**Cause** — a defect in EF Core's own materializer, reached because this provider rebuilds entities
on the server from the values sent over the wire. Tracked upstream as
[dotnet/efcore#36175](https://github.com/dotnet/efcore/issues/36175).

---

## Use with caution

### Three-level correlated collections with `Distinct`

**Affects you if** you project nested collections three levels deep and apply `Distinct` to the
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

**What happens** — the query runs and returns a result. **No EF Core provider supports this
query**: SQL Server, SQLite and InMemory all reject it. Because no provider executes it, there is
no reference answer to check ours against, so the result this provider returns is unverified.

**Workaround** — apply `Distinct` after materialising:

```csharp
Products = o.Lines.Select(l => l.ProductName).ToList(),   // then .Distinct() in memory
```

---

## Differences that are not limitations

These behave correctly. They are listed because the behaviour differs from another EF Core
provider, and you may notice the difference when porting code or tests.

### Exception message text for an untranslatable query

When a query cannot be translated, this provider throws `InvalidOperationException`, exactly as EF
Core does. **The message text may differ** in two cases — a method call inside an `ExecuteUpdate`
property selector, and a cast to a type nothing in your model implements:

```csharp
// (a) a method call where ExecuteUpdate expects a property
context.Orders
    .Where(o => o.Total > 100m)
    .ExecuteUpdate(s => s.SetProperty(o => Math.Round(o.Total), 0m));

// (b) a cast to a type no mapped entity implements
IQueryable orders = context.Orders;
orders.Cast<IArchivable>().FirstOrDefault();
```

Both throw. **Catch the exception type; do not match on message text** — that is unsupported on any
EF Core provider.

### Queries this provider answers that other providers reject

Two scenarios in EF's suite assert that a provider *rejects* the query. This provider answers them
correctly instead. There is nothing to do about this; it is noted so that a test suite you port
from another provider, which expects an exception, does not surprise you.

**Composing LINQ over a collection stored through a value converter:**

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

**`Contains` over a collection of enums stored as a string:**

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

---

## How this page is maintained

Every entry above corresponds to tests in EF Core's specification suite that run on every build.
The number of failing tests is gated in continuous integration, so it cannot grow without being
noticed. When an entry is fixed, or a new one appears, this page changes in the same commit.
