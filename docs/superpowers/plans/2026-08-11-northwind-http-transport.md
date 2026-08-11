# Northwind Sample — Phase 1: the HTTP transport

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A `DbContext` with no database answers queries, saves changes and runs a transaction against a SQLite-backed ASP.NET Core server **over real HTTP**, proved by tests, with no browser involved.

**Architecture:** Three new projects. `samples/Northwind.Shared` holds one `NorthwindContext` used by both sides, because the wire carries entity type *names* and the two models must agree (A49). `samples/Northwind.Server` hosts an endpoint that feeds the existing `InfoCarrierEnvelopeServer`. A new `HttpInfoCarrierTransport` implements the existing one-method `IInfoCarrierTransport` seam. A new test project drives it end to end through `WebApplicationFactory`.

**Tech Stack:** .NET 10, EF Core 10, ASP.NET Core minimal APIs, SQLite, xUnit, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** [`docs/superpowers/specs/2026-08-11-blazor-wasm-sample-design.md`](../specs/2026-08-11-blazor-wasm-sample-design.md). This plan implements its **Phase 1** only (spec §10). Phase 2 (Blazor WASM, Fluent UI, inspector, trimming) gets its own plan.

## Global Constraints

Every task's requirements implicitly include these.

- **Target framework `net10.0`.** Set once in `Directory.Build.props`; do not set it per project.
- **Central package management is on.** A new package needs a `<PackageVersion>` entry in `Directory.Packages.props` **and** a `<PackageReference>` without a version in the csproj.
- **Sample and test projects set `<GenerateDocumentationFile>false</GenerateDocumentationFile>`.** `Directory.Build.props` sets it true for the product; samples do not need XML docs and the missing-doc warnings are noise.
- **Sample projects set `<IsPackable>false</IsPackable>`.**
- **Never edit anything under `subrepos/`.** They are git-ignored reference clones.
- **No NuGet dependency on Remote.Linq or Aqua** (ADR-001).
- **EF1001 warnings are expected and allowed.** Do not suppress them repo-wide.
- **The two transport files carry no sample types.** `HttpInfoCarrierTransport.cs` and `InfoCarrierEndpointExtensions.cs` must not mention Northwind, so that promoting them to packages later is a file move (spec §4.1). A reviewer checks this by reading their `using` lines.
- **One task per commit.** Commit message prefixed `Step M8-<n>:`. Tick this plan's checkboxes and add the matching entry to `docs/implementation-plan.md` **in the same commit** — the repo has been bitten by those two drifting apart. **Stage this plan file as well as `implementation-plan.md`**; the per-task `git add` lines below name the source files and do not repeat these two.
- **Do not run the full spec suite unless a task says to.** It takes 6–9 minutes. Only Tasks 1, 2 and 7 call for `eng/measure.sh`, and each states the exact expected output.
- **Report test results as `Passed: N, Failed: M, Total: T` read from actual output.** Never estimate or infer a count.

---

## File Structure

| File | Responsibility |
|---|---|
| `eng/measure.sh` *(modify)* | Scope the spec measurement to one project. |
| `docs/implementation-plan.md` *(replace)* | M8's rolling checkbox record; Phase C's is archived. |
| `samples/Northwind.Shared/Model/*.cs` | Five POCOs. No EF configuration, no behaviour. |
| `samples/Northwind.Shared/NorthwindContext.cs` | The one shared context and its `OnModelCreating`. |
| `samples/Northwind.Shared/NorthwindSeed.cs` | Deterministic seed data. |
| `samples/Northwind.Server/Transport/InfoCarrierEndpointExtensions.cs` | `MapInfoCarrier()`. **No sample types.** |
| `samples/Northwind.Server/Program.cs` | DI wiring, SQLite, seeding, endpoint mapping. |
| `samples/Northwind.Client.Transport/HttpInfoCarrierTransport.cs` | `IInfoCarrierTransport` over `HttpClient`. **No sample types.** |
| `samples/Northwind.Client.Transport/InfoCarrierTransportException.cs` | The transport's own failure type. |
| `test/InfoCarrier.Core.TransportTests/**` | All Phase 1 tests. |

**Why `Northwind.Client.Transport` is its own project rather than living in the server:** Phase 2's Blazor client references it, and the server must not be a dependency of the browser. It is a plain class library with `System.Net.Http` only, which is what makes it WASM-safe.

---

### Task 1: Prepare the ground — scope the measurement, open the M8 plan

**Why this is first.** `eng/measure.sh` runs `dotnet test` against the **solution** and then takes the *last* `Total tests:` block it finds:

```bash
summary=$(grep -E "^Total tests:" "$log" | tail -n 1 || true)
```

Adding a second test project to the solution makes `dotnet test` emit two summary blocks, and `tail -n 1` would pick whichever project finished last. Every measurement after that would be silently wrong. Fix the instrument before adding the project.

`eng/ratchet.sh` needs no change: CI already runs `dotnet test ${{ env.TEST_PROJECT }}`, which is project-scoped.

**Files:**
- Modify: `eng/measure.sh:43`
- Modify: `docs/implementation-plan.md` (replace with the M8 plan)
- Create: `docs/archive/implementation-plan-m6-phase-c.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a project-scoped `eng/measure.sh`; `docs/implementation-plan.md` with an M8 section whose entries later tasks tick.

- [x] **Step 1: Record the current baseline number so the change can be proved neutral**

Run: `cat artifacts/measure/c96.txt | wc -l && tail -2 test/known-failures.txt`

Expected: `13`, then `failed=13` and `total=22453`.

- [x] **Step 2: Scope `measure.sh` to the functional test project**

In `eng/measure.sh`, change the `dotnet test` line (currently line 43):

```bash
dotnet test "$root/InfoCarrier.Core.slnx" --no-build -v n --nologo > "$log" 2>&1 || true
```

to:

```bash
# The **project**, not the solution. This script measures the inherited spec suite, which is
# one project; the summary block is parsed with `tail -n 1`, so a second test project in the
# solution would silently make every measurement report the wrong run. Phase 1 of M8 adds
# exactly such a project (InfoCarrier.Core.TransportTests), so this is scoped first and proved
# neutral before that project exists. eng/ratchet.sh needs no equivalent change: CI has always
# run `dotnet test $TEST_PROJECT`.
dotnet test "$root/test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj" \
    --no-build -v n --nologo > "$log" 2>&1 || true
```

- [x] **Step 3: Prove the change is neutral**

Run: `bash eng/measure.sh c97-instrument c96`

Expected, exactly:

```
FAILING: 13  TOTAL: 22453  (c97-instrument)

FIXED  (in c96, not in c97-instrument):
BROKEN (in c97-instrument, not in c96):
```

The REASONS diff must be empty. **If any line differs, stop.** The instrument changed the reading, which means the old reading included something the new one does not, and that must be understood before anything is built on top of it.

- [x] **Step 4: Archive Phase C's plan and open M8's**

```bash
git mv docs/implementation-plan.md docs/archive/implementation-plan-m6-phase-c.md
```

Create a new `docs/implementation-plan.md`:

```markdown
# Implementation plan — M8 (productization)

Rolling checkbox detail for the **current** milestone only. M6's plan (Phases A–C) is in
[`archive/implementation-plan-m6-phase-c.md`](archive/implementation-plan-m6-phase-c.md) and is
never edited again.

Milestone-level scope lives in [`roadmap.md`](roadmap.md). Do not put scope here.

The suite stands at `Total tests: 22453, Passed: 22219, Failed: 13, Skipped: 221` (`c96`). All 13
are classified in C96 of the archived plan; none is a blocker for M8.

## Phase H — the HTTP transport (spec: `superpowers/specs/2026-08-11-blazor-wasm-sample-design.md` §10 phase 1)

Detailed steps are in
[`superpowers/plans/2026-08-11-northwind-http-transport.md`](superpowers/plans/2026-08-11-northwind-http-transport.md).
**That document is the "how"; this one is the record of what landed and what it measured.**

- [ ] **M8-1. The spec measurement is scoped to one project, and the M8 plan is open.** `<this commit>`
```

- [x] **Step 5: Commit**

```bash
git add eng/measure.sh docs/implementation-plan.md docs/archive/implementation-plan-m6-phase-c.md
git commit -m "Step M8-1: scope the spec measurement to one project, and open the M8 plan

eng/measure.sh ran dotnet test against the whole solution and parsed the LAST
'Total tests:' block. Phase 1 of M8 adds a second test project, which would have
made that line pick whichever project finished last and silently corrupted every
measurement after it. Scoped to the functional test project and proved neutral:
FAILING: 13 TOTAL: 22453, with empty FIXED, BROKEN and REASONS diffs against c96.

eng/ratchet.sh needs no change -- CI has always run a single project.

M6's plan is archived and docs/implementation-plan.md is reopened for M8."
```

---

### Task 2: The shared model and the test project

**Files:**
- Create: `samples/Northwind.Shared/Northwind.Shared.csproj`
- Create: `samples/Northwind.Shared/Model/Category.cs`, `Customer.cs`, `Order.cs`, `OrderDetail.cs`, `Product.cs`
- Create: `samples/Northwind.Shared/NorthwindContext.cs`
- Create: `samples/Northwind.Shared/NorthwindSeed.cs`
- Create: `test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`
- Create: `test/InfoCarrier.Core.TransportTests/NorthwindModelTest.cs`
- Modify: `InfoCarrier.Core.slnx`
- Modify: `Directory.Packages.props`

**Interfaces:**
- Consumes: nothing.
- Produces: `NorthwindContext : DbContext` with `DbSet<Customer> Customers`, `DbSet<Order> Orders`, `DbSet<OrderDetail> OrderDetails`, `DbSet<Product> Products`, `DbSet<Category> Categories`; and `static void NorthwindSeed.Seed(NorthwindContext context)`.

- [x] **Step 1: Add package versions**

In `Directory.Packages.props`, inside the existing `<ItemGroup>`:

```xml
<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Proxies" Version="10.0.0" />
```

- [x] **Step 2: Create the shared project**

`samples/Northwind.Shared/Northwind.Shared.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <Description>Shared Northwind model and DbContext for the InfoCarrier sample. Used by BOTH halves: the wire carries entity type names, so the two models must agree (A49).</Description>
    <RootNamespace>Northwind.Shared</RootNamespace>
    <AssemblyName>Northwind.Shared</AssemblyName>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />

    <!--
      Automatic lazy loading (spec 3.2). Referenced explicitly: the functional test project gets
      this transitively through the spec-tests package, which a sample must not reference.
    -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Proxies" />
  </ItemGroup>

</Project>
```

- [x] **Step 3: Write the POCOs**

`samples/Northwind.Shared/Model/Category.cs`:

```csharp
namespace Northwind.Shared.Model;

public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
```

`samples/Northwind.Shared/Model/Customer.cs`:

```csharp
namespace Northwind.Shared.Model;

public class Customer
{
    public string Id { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string City { get; set; } = null!;

    public string Country { get; set; } = null!;
}
```

`samples/Northwind.Shared/Model/Product.cs`:

```csharp
namespace Northwind.Shared.Model;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int UnitsInStock { get; set; }

    public int CategoryId { get; set; }
}
```

`samples/Northwind.Shared/Model/Order.cs`. **Navigations are `virtual`**, which is what
`UseLazyLoadingProxies()` requires — automatic lazy loading, no ceremony in the model (spec §3.2).

```csharp
namespace Northwind.Shared.Model;

public class Order
{
    public int Id { get; set; }

    public string CustomerId { get; set; } = null!;

    public DateTime OrderDate { get; set; }

    // `virtual`, so the proxy can override it. Automatic lazy loading is the target; if it turns
    // out not to work in the browser, spec 3.2 records the ILazyLoader fallback and it is a
    // change to this folder only.
    public virtual Customer? Customer { get; set; }

    public virtual List<OrderDetail> OrderDetails { get; set; } = [];
}
```

`samples/Northwind.Shared/Model/OrderDetail.cs`:

```csharp
namespace Northwind.Shared.Model;

public class OrderDetail
{
    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public virtual Product? Product { get; set; }
}
```

- [x] **Step 4: Write the context**

`samples/Northwind.Shared/NorthwindContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Northwind.Shared.Model;

namespace Northwind.Shared;

/// <summary>
///     The one context type both halves use.
/// </summary>
/// <remarks>
///     Not a convenience. The wire carries entity type NAMES, so the client's model and the
///     server's must agree about them; one shared OnModelCreating makes that true by
///     construction rather than by discipline. See A49 in CLAUDE.md.
/// </remarks>
public class NorthwindContext(DbContextOptions<NorthwindContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().Property(e => e.Id).HasMaxLength(5).ValueGeneratedNever();

        modelBuilder.Entity<OrderDetail>().HasKey(e => new { e.OrderId, e.ProductId });

        modelBuilder.Entity<Order>()
            .HasMany(e => e.OrderDetails)
            .WithOne()
            .HasForeignKey(e => e.OrderId);

        modelBuilder.Entity<Order>()
            .HasOne(e => e.Customer)
            .WithMany()
            .HasForeignKey(e => e.CustomerId);

        modelBuilder.Entity<OrderDetail>()
            .HasOne(e => e.Product)
            .WithMany()
            .HasForeignKey(e => e.ProductId);
    }
}
```

- [x] **Step 5: Write the seed**

`samples/Northwind.Shared/NorthwindSeed.cs`:

```csharp
using Northwind.Shared.Model;

namespace Northwind.Shared;

/// <summary>
///     Deterministic seed data. Small on purpose: the sample demonstrates a wire protocol, not
///     a data set.
/// </summary>
public static class NorthwindSeed
{
    public static void Seed(NorthwindContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Customers.Any())
        {
            return;
        }

        context.Categories.AddRange(
            new Category { Id = 1, Name = "Beverages" },
            new Category { Id = 2, Name = "Condiments" },
            new Category { Id = 3, Name = "Confections" });

        context.Products.AddRange(
            new Product { Id = 1, Name = "Chai", UnitPrice = 18.00m, UnitsInStock = 39, CategoryId = 1 },
            new Product { Id = 2, Name = "Chang", UnitPrice = 19.00m, UnitsInStock = 17, CategoryId = 1 },
            new Product { Id = 3, Name = "Aniseed Syrup", UnitPrice = 10.00m, UnitsInStock = 13, CategoryId = 2 },
            new Product { Id = 4, Name = "Chef Anton's Cajun Seasoning", UnitPrice = 22.00m, UnitsInStock = 53, CategoryId = 2 },
            new Product { Id = 5, Name = "Pavlova", UnitPrice = 17.45m, UnitsInStock = 29, CategoryId = 3 },
            new Product { Id = 6, Name = "Teatime Chocolate Biscuits", UnitPrice = 9.20m, UnitsInStock = 25, CategoryId = 3 });

        context.Customers.AddRange(
            new Customer { Id = "ALFKI", CompanyName = "Alfreds Futterkiste", City = "Berlin", Country = "Germany" },
            new Customer { Id = "ANATR", CompanyName = "Ana Trujillo Emparedados", City = "México D.F.", Country = "Mexico" },
            new Customer { Id = "AROUT", CompanyName = "Around the Horn", City = "London", Country = "UK" },
            new Customer { Id = "BERGS", CompanyName = "Berglunds snabbköp", City = "Luleå", Country = "Sweden" });

        context.Orders.AddRange(
            new Order { Id = 1, CustomerId = "ALFKI", OrderDate = new DateTime(2026, 1, 5) },
            new Order { Id = 2, CustomerId = "ALFKI", OrderDate = new DateTime(2026, 2, 11) },
            new Order { Id = 3, CustomerId = "ANATR", OrderDate = new DateTime(2026, 2, 18) },
            new Order { Id = 4, CustomerId = "AROUT", OrderDate = new DateTime(2026, 3, 2) },
            new Order { Id = 5, CustomerId = "BERGS", OrderDate = new DateTime(2026, 3, 20) });

        context.OrderDetails.AddRange(
            new OrderDetail { OrderId = 1, ProductId = 1, UnitPrice = 18.00m, Quantity = 12 },
            new OrderDetail { OrderId = 1, ProductId = 5, UnitPrice = 17.45m, Quantity = 3 },
            new OrderDetail { OrderId = 2, ProductId = 2, UnitPrice = 19.00m, Quantity = 5 },
            new OrderDetail { OrderId = 3, ProductId = 3, UnitPrice = 10.00m, Quantity = 20 },
            new OrderDetail { OrderId = 4, ProductId = 4, UnitPrice = 22.00m, Quantity = 7 },
            new OrderDetail { OrderId = 4, ProductId = 6, UnitPrice = 9.20m, Quantity = 10 },
            new OrderDetail { OrderId = 5, ProductId = 1, UnitPrice = 18.00m, Quantity = 2 });

        context.SaveChanges();
    }
}
```

- [x] **Step 6: Create the test project**

`test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <Description>Tests for the HTTP transport and the sample's shared model. Deliberately NOT part of InfoCarrier.Core.FunctionalTests: that project is what eng/ratchet.sh counts and what test/known-failures.txt describes, and its number must keep meaning "inherited spec tests failing".</Description>
    <RootNamespace>InfoCarrier.Core.TransportTests</RootNamespace>
    <AssemblyName>InfoCarrier.Core.TransportTests</AssemblyName>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\InfoCarrier.Core\InfoCarrier.Core.csproj" />
    <ProjectReference Include="..\..\samples\Northwind.Shared\Northwind.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

</Project>
```

- [x] **Step 7: Write the failing test**

`test/InfoCarrier.Core.TransportTests/NorthwindModelTest.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Northwind.Shared;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NorthwindModelTest
{
    [Fact]
    public void The_model_declares_the_five_entity_types_the_wire_will_name()
    {
        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseSqlite("Filename=:memory:")
                .UseLazyLoadingProxies()
                .Options);

        string[] names = context.Model.GetEntityTypes()
            .Select(e => e.ClrType.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["Category", "Customer", "Order", "OrderDetail", "Product"], names);
    }

    [Fact]
    public void An_order_detail_is_keyed_by_order_and_product()
    {
        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseSqlite("Filename=:memory:")
                .UseLazyLoadingProxies()
                .Options);

        IKey key = context.Model.FindEntityType(typeof(Northwind.Shared.Model.OrderDetail))!.FindPrimaryKey()!;

        Assert.Equal(["OrderId", "ProductId"], key.Properties.Select(p => p.Name));
    }

    [Fact]
    public void The_seed_is_idempotent()
    {
        DbContextOptions<NorthwindContext> options = new DbContextOptionsBuilder<NorthwindContext>()
            .UseSqlite("Filename=:memory:")
            .Options;

        // One open connection keeps an in-memory SQLite database alive for the test's lifetime.
        using var context = new NorthwindContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        NorthwindSeed.Seed(context);
        int afterFirst = context.Customers.Count();

        NorthwindSeed.Seed(context);

        Assert.Equal(4, afterFirst);
        Assert.Equal(afterFirst, context.Customers.Count());
    }
}
```

- [x] **Step 8: Add both projects to the solution**

Replace `InfoCarrier.Core.slnx` with:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/InfoCarrier.Core.Abstractions/InfoCarrier.Core.Abstractions.csproj" />
    <Project Path="src/InfoCarrier.Core/InfoCarrier.Core.csproj" />
  </Folder>
  <Folder Name="/samples/">
    <Project Path="samples/Northwind.Shared/Northwind.Shared.csproj" />
  </Folder>
  <Folder Name="/test/">
    <Project Path="test/InfoCarrier.Core.FunctionalTests/InfoCarrier.Core.FunctionalTests.csproj" />
    <Project Path="test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj" />
  </Folder>
</Solution>
```

- [x] **Step 9: Run the tests**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

Expected: `Passed: 3, Failed: 0, Total: 3`.

- [x] **Step 10: Prove the spec measurement is untouched**

Run: `bash eng/measure.sh m8-2 c96`

Expected: `FAILING: 13  TOTAL: 22453`, with empty FIXED, BROKEN and REASONS diffs. This is the check that Task 1 was worth doing.

- [x] **Step 11: Commit**

```bash
git add samples/ test/InfoCarrier.Core.TransportTests/ InfoCarrier.Core.slnx Directory.Packages.props docs/implementation-plan.md
git commit -m "Step M8-2: the shared Northwind model, and a test project of its own

One NorthwindContext used by both halves, because the wire carries entity type
names and the two models must agree (A49). This is the first worked example of
D2 in architecture.md -- one model configuration both halves derive from -- and
it is worth noting how small it is: about a dozen lines of OnModelCreating for
five entity types, because EF's conventions produce the rest.

Navigations are virtual and lazy loading is automatic via UseLazyLoadingProxies,
enabled on both halves because proxies add a model convention and the two models
must agree. Whether proxies survive a browser is a phase 2 experiment; spec 3.2
records the ILazyLoader fallback and it is a change to this folder only.

The tests live in a new project rather than in InfoCarrier.Core.FunctionalTests,
so the ratchet's number keeps meaning 'inherited spec tests failing'.

Passed: 3, Failed: 0, Total: 3. Spec suite unchanged at 13/22453."
```

---

### Task 3: `HttpInfoCarrierTransport`

Tested against a stub `HttpMessageHandler`, so no server is needed yet.

**Files:**
- Create: `samples/Northwind.Client.Transport/Northwind.Client.Transport.csproj`
- Create: `samples/Northwind.Client.Transport/HttpInfoCarrierTransport.cs`
- Create: `samples/Northwind.Client.Transport/InfoCarrierTransportException.cs`
- Create: `test/InfoCarrier.Core.TransportTests/HttpInfoCarrierTransportTest.cs`
- Modify: `InfoCarrier.Core.slnx`, `test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

**Interfaces:**
- Consumes: `IInfoCarrierTransport`, `InfoCarrierEnvelope`, `IInfoCarrierSerializer` from `InfoCarrier.Core`.
- Produces: `HttpInfoCarrierTransport(HttpClient httpClient, IInfoCarrierSerializer serializer, string requestUri = "infocarrier")` implementing `Task<InfoCarrierEnvelope> SendAsync(InfoCarrierEnvelope, CancellationToken)`; and `InfoCarrierTransportException : Exception`.

- [x] **Step 1: Write the failing test**

`test/InfoCarrier.Core.TransportTests/HttpInfoCarrierTransportTest.cs`:

```csharp
using System.Net;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Northwind.Client.Transport;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class HttpInfoCarrierTransportTest
{
    private static readonly IInfoCarrierSerializer Serializer = new SystemTextJsonInfoCarrierSerializer();

    private static InfoCarrierEnvelope AnEnvelope()
        => new()
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.BeginTransaction,
            Payload = Serializer.Serialize<object?>(null),
        };

    [Fact]
    public async Task It_posts_to_the_configured_relative_uri()
    {
        Uri? seen = null;
        var handler = new StubHandler(request =>
        {
            seen = request.RequestUri;
            return Respond(AnEnvelope());
        });

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        await transport.SendAsync(AnEnvelope());

        Assert.Equal("https://example.test/infocarrier", seen?.ToString());
    }

    [Fact]
    public async Task It_round_trips_an_envelope()
    {
        InfoCarrierEnvelope expected = AnEnvelope() with { CorrelationId = "abc-123" };
        var handler = new StubHandler(_ => Respond(expected));

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierEnvelope actual = await transport.SendAsync(AnEnvelope());

        Assert.Equal("abc-123", actual.CorrelationId);
        Assert.Equal(InfoCarrierOperation.BeginTransaction, actual.Operation);
    }

    [Fact]
    public async Task A_non_success_status_is_reported_with_the_status_and_the_body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream is down"),
        });

        var transport = new HttpInfoCarrierTransport(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }, Serializer);

        InfoCarrierTransportException exception =
            await Assert.ThrowsAsync<InfoCarrierTransportException>(() => transport.SendAsync(AnEnvelope()));

        Assert.Contains("502", exception.Message);
        Assert.Contains("upstream is down", exception.Message);
    }

    private static HttpResponseMessage Respond(InfoCarrierEnvelope envelope)
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Serializer.Serialize(envelope)) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
```

- [x] **Step 2: Run it to verify it fails**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --filter "FullyQualifiedName~HttpInfoCarrierTransportTest"`

Expected: FAIL to compile — `The type or namespace name 'Northwind' could not be found` (the transport project does not exist yet).

- [x] **Step 3: Create the transport project**

`samples/Northwind.Client.Transport/Northwind.Client.Transport.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <Description>An IInfoCarrierTransport over HttpClient. Contains no sample types: this is written to be promoted into an InfoCarrier.Core.Http package, at which point the move is a file move. System.Net.Http only, so it is safe in WebAssembly.</Description>
    <RootNamespace>Northwind.Client.Transport</RootNamespace>
    <AssemblyName>Northwind.Client.Transport</AssemblyName>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\InfoCarrier.Core\InfoCarrier.Core.csproj" />
  </ItemGroup>

</Project>
```

- [x] **Step 4: Write the exception type**

`samples/Northwind.Client.Transport/InfoCarrierTransportException.cs`:

```csharp
namespace Northwind.Client.Transport;

/// <summary>
///     A failure of the transport itself, as opposed to a failure the server reported.
/// </summary>
/// <remarks>
///     The distinction matters and is why this type exists. A server-side failure travels as
///     data in <c>InfoCarrierEnvelope.Fault</c> and is raised again on the client with its
///     original type (W5). This is the other case: the request never reached a server, or what
///     came back was not an envelope. Reporting that as an EF exception would be a lie about
///     where the fault is.
/// </remarks>
public sealed class InfoCarrierTransportException : Exception
{
    public InfoCarrierTransportException(string message)
        : base(message)
    {
    }

    public InfoCarrierTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [x] **Step 5: Write the transport**

`samples/Northwind.Client.Transport/HttpInfoCarrierTransport.cs`:

```csharp
using System.Net.Http.Headers;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

namespace Northwind.Client.Transport;

/// <summary>
///     Carries an <see cref="InfoCarrierEnvelope" /> to a server over HTTP.
/// </summary>
/// <remarks>
///     <para>
///         The whole transport seam is one method, so this is the whole transport. The server
///         half is the <c>MapInfoCarrier</c> endpoint, which hands the envelope to the product's
///         existing <c>InfoCarrierEnvelopeServer</c>.
///     </para>
///     <para>
///         <b>Deliberately free of sample types</b>, so that promoting it into an
///         <c>InfoCarrier.Core.Http</c> package is a file move (spec 4.1). Nothing here references
///         Northwind, and nothing here needs ASP.NET.
///     </para>
/// </remarks>
public sealed class HttpInfoCarrierTransport : IInfoCarrierTransport
{
    private readonly HttpClient _httpClient;
    private readonly IInfoCarrierSerializer _serializer;
    private readonly string _requestUri;

    public HttpInfoCarrierTransport(
        HttpClient httpClient,
        IInfoCarrierSerializer serializer,
        string requestUri = "infocarrier")
    {
        _httpClient = httpClient;
        _serializer = serializer;
        _requestUri = requestUri;
    }

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        byte[] body = await _serializer.SerializeAsync(request, cancellationToken).ConfigureAwait(false);

        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using HttpResponseMessage response =
            await _httpClient.PostAsync(_requestUri, content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            // Both the status and the body. A transport failure reported as a bare status code
            // is indistinguishable from a dozen unrelated causes to whoever has to diagnose it.
            string detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered {(int)response.StatusCode} "
                + $"({response.ReasonPhrase}). Body: {detail}");
        }

        byte[] responseBody =
            await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        return await _serializer.DeserializeAsync<InfoCarrierEnvelope>(responseBody, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new InfoCarrierTransportException(
                $"The InfoCarrier server at '{_requestUri}' answered 200 with a body that is not an "
                + $"envelope ({responseBody.Length} bytes).");
    }
}
```

- [x] **Step 6: Reference the transport from the test project and the solution**

Add to `test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`, in the `ProjectReference` group:

```xml
<ProjectReference Include="..\..\samples\Northwind.Client.Transport\Northwind.Client.Transport.csproj" />
```

Add to `InfoCarrier.Core.slnx`, in the `/samples/` folder:

```xml
<Project Path="samples/Northwind.Client.Transport/Northwind.Client.Transport.csproj" />
```

- [x] **Step 7: Run the tests**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

Expected: `Passed: 6, Failed: 0, Total: 6`.

- [x] **Step 8: Verify the promotion constraint by hand**

Run: `grep -n "^using" samples/Northwind.Client.Transport/*.cs`

Expected: no `using` mentions `Northwind.Shared`, ASP.NET, or EF Core. Only `System.Net.Http.Headers`, `InfoCarrier.Core` and `InfoCarrier.Core.Common`. **If any sample type appears, fix it now** — the constraint is cheap to hold and expensive to restore.

- [x] **Step 9: Commit**

```bash
git add samples/Northwind.Client.Transport/ test/InfoCarrier.Core.TransportTests/ InfoCarrier.Core.slnx docs/implementation-plan.md
git commit -m "Step M8-3: an IInfoCarrierTransport over HttpClient

The transport seam is one method, so this is the whole client half. Tested
against a stub HttpMessageHandler, so no server is needed yet: it posts to the
configured relative URI, round-trips an envelope, and reports a non-success
status with both the status and the body.

InfoCarrierTransportException is a distinct type on purpose. A server-side
failure travels as data in the envelope's Fault and is raised again with its
original type (W5); this is the other case, where the request never reached a
server or the answer was not an envelope.

No sample types: promoting this to InfoCarrier.Core.Http is a file move.

Passed: 6, Failed: 0, Total: 6."
```

- [x] **Fix round 1 (review, Step M8-3a): the malformed-body path.** Code review found that
  `SendAsync` only wrapped the case where `DeserializeAsync` returned `null`, not the case where
  it *threw* -- a non-JSON 200 body (misconfigured proxy, captive portal) surfaced a raw
  `JsonException` instead of the documented `InfoCarrierTransportException`. Fixed by wrapping the
  deserialize call and rethrowing via the exception type's previously-unused two-argument
  constructor, explicitly letting `OperationCanceledException` pass through uncaught. Covered by a
  fourth test, `A_200_body_that_is_not_an_envelope_is_reported_as_a_transport_failure`. Project
  total 6 -> 7.

---

### Task 4: The server endpoint

**Files:**
- Create: `samples/Northwind.Server/Northwind.Server.csproj`
- Create: `samples/Northwind.Server/Transport/InfoCarrierEndpointExtensions.cs`
- Create: `samples/Northwind.Server/Program.cs`
- Create: `test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs`
- Create: `test/InfoCarrier.Core.TransportTests/InfoCarrierEndpointTest.cs`
- Modify: `InfoCarrier.Core.slnx`, `test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`, `Directory.Packages.props`

**Interfaces:**
- Consumes: `HttpInfoCarrierTransport` from Task 3; `NorthwindContext`, `NorthwindSeed` from Task 2.
- Produces: `IEndpointRouteBuilder.MapInfoCarrier(string pattern = "infocarrier")`; a `public partial class Program` entry point usable as `WebApplicationFactory<Program>`; and `NorthwindServerFactory : WebApplicationFactory<Program>` exposing `HttpClient CreateInfoCarrierClient()`.

- [x] **Step 1: Write the failing test**

`test/InfoCarrier.Core.TransportTests/InfoCarrierEndpointTest.cs`:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Northwind.Client.Transport;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class InfoCarrierEndpointTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    private static readonly IInfoCarrierSerializer Serializer = new SystemTextJsonInfoCarrierSerializer();

    [Fact]
    public async Task A_begin_transaction_envelope_comes_back_with_a_transaction_id()
    {
        var transport = new HttpInfoCarrierTransport(factory.CreateClient(), Serializer);

        InfoCarrierEnvelope response = await transport.SendAsync(
            new InfoCarrierEnvelope
            {
                ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
                Operation = InfoCarrierOperation.BeginTransaction,
                Payload = Serializer.Serialize<object?>(null),
            });

        Assert.Null(response.Fault);

        TransactionResult? result = Serializer.Deserialize<TransactionResult>(response.Payload);
        Assert.False(string.IsNullOrEmpty(result?.TransactionId));
    }

    [Fact]
    public async Task An_unsupported_protocol_version_is_refused_by_number()
    {
        var transport = new HttpInfoCarrierTransport(factory.CreateClient(), Serializer);

        InfoCarrierTransportException exception = await Assert.ThrowsAsync<InfoCarrierTransportException>(
            () => transport.SendAsync(
                new InfoCarrierEnvelope
                {
                    ProtocolVersion = 999,
                    Operation = InfoCarrierOperation.BeginTransaction,
                    Payload = Serializer.Serialize<object?>(null),
                }));

        Assert.Contains("999", exception.Message);
    }
}
```

- [x] **Step 2: Run it to verify it fails**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --filter "FullyQualifiedName~InfoCarrierEndpointTest"`

Expected: FAIL to compile — `NorthwindServerFactory` does not exist.

- [x] **Step 3: Add the test-host package**

In `Directory.Packages.props`, the entry added in Task 2 Step 1 covers this. Add to `test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
```

and a project reference:

```xml
<ProjectReference Include="..\..\samples\Northwind.Server\Northwind.Server.csproj" />
```

- [x] **Step 4: Create the server project**

`samples/Northwind.Server/Northwind.Server.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <Description>The InfoCarrier sample server: SQLite behind an HTTP endpoint that speaks the envelope protocol.</Description>
    <RootNamespace>Northwind.Server</RootNamespace>
    <AssemblyName>Northwind.Server</AssemblyName>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\InfoCarrier.Core\InfoCarrier.Core.csproj" />
    <ProjectReference Include="..\Northwind.Shared\Northwind.Shared.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" />
  </ItemGroup>

</Project>
```

- [x] **Step 5: Write the endpoint**

`samples/Northwind.Server/Transport/InfoCarrierEndpointExtensions.cs`:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Northwind.Server.Transport;

/// <summary>
///     Maps the InfoCarrier envelope endpoint.
/// </summary>
/// <remarks>
///     <para>
///         All nine operations, one route. The product's <see cref="InfoCarrierEnvelopeServer" />
///         already checks the protocol version, dispatches, and turns a server-side failure into
///         a fault carried in the response (W5), so this adds no policy of its own.
///     </para>
///     <para>
///         <b>Deliberately free of sample types</b>, so promoting it into an
///         <c>InfoCarrier.Core.AspNetCore</c> package is a file move (spec 4.1).
///     </para>
/// </remarks>
public static class InfoCarrierEndpointExtensions
{
    public static IEndpointConventionBuilder MapInfoCarrier(
        this IEndpointRouteBuilder endpoints,
        string pattern = "infocarrier")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapPost(pattern, async (HttpContext http) =>
        {
            IInfoCarrierSerializer serializer = http.RequestServices.GetRequiredService<IInfoCarrierSerializer>();

            using var buffer = new MemoryStream();
            await http.Request.Body.CopyToAsync(buffer, http.RequestAborted).ConfigureAwait(false);

            InfoCarrierEnvelope request =
                serializer.Deserialize<InfoCarrierEnvelope>(buffer.ToArray())
                ?? throw new InvalidOperationException("The request body is not an InfoCarrier envelope.");

            var envelopeServer = new InfoCarrierEnvelopeServer(
                http.RequestServices.GetRequiredService<IInfoCarrierServer>(), serializer);

            InfoCarrierEnvelope response =
                await envelopeServer.DispatchAsync(request, http.RequestAborted).ConfigureAwait(false);

            http.Response.ContentType = "application/json";
            await http.Response.Body.WriteAsync(serializer.Serialize(response), http.RequestAborted)
                .ConfigureAwait(false);
        });
    }
}
```

- [x] **Step 6: Write `Program.cs`**

`samples/Northwind.Server/Program.cs`:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Northwind.Server.Transport;
using Northwind.Shared;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string databasePath = Path.Combine(AppContext.BaseDirectory, "northwind.db");

builder.Services.AddDbContext<NorthwindContext>(
    options => options
        .UseSqlite($"Filename={databasePath}")

        // Enabled on BOTH halves. Proxies add a model convention, and the two models must agree
        // about everything the wire names (A49, and D2 in architecture.md).
        .UseLazyLoadingProxies());

// The server resolves its DbContext per request from this provider, so `DbContext` itself must
// be resolvable — `InProcessInfoCarrierServer` asks for the base type, not for NorthwindContext.
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<NorthwindContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()

    // ADR-012, amended C89. The client gets the standard value mappers from
    // AddEntityFrameworkInfoCarrier; a server builds its own service collection, so it has to
    // ask. A value mapped on one side only is worse than one mapped on neither.
    .AddInfoCarrierStandardValueMappers();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<NorthwindContext>();
    context.Database.EnsureCreated();
    NorthwindSeed.Seed(context);
}

app.MapInfoCarrier();

app.Run();

/// <summary>
///     Named so that <c>WebApplicationFactory&lt;Program&gt;</c> can find an entry point. A
///     top-level-statements program has an internal one otherwise.
/// </summary>
public partial class Program;
```

- [x] **Step 7: Write the test factory**

`test/InfoCarrier.Core.TransportTests/NorthwindServerFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     Hosts the sample server in-process and gives each test class its own SQLite file.
/// </summary>
/// <remarks>
///     A file, not <c>Mode=Memory;Cache=Shared</c>. CLAUDE.md records that making a database's
///     lifetime a connection's has already produced a 698-test phantom failure in this repo, and
///     the reason applies here too.
/// </remarks>
public sealed class NorthwindServerFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"northwind-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.ConfigureServices(services =>
        {
            ServiceDescriptor descriptor = services.Single(
                d => d.ServiceType == typeof(DbContextOptions<NorthwindContext>));
            services.Remove(descriptor);

            services.AddDbContext<NorthwindContext>(
                options => options
                    .UseSqlite($"Filename={_databasePath}")
                    .UseLazyLoadingProxies());
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
```

- [x] **Step 8: Add the server to the solution**

Add to `InfoCarrier.Core.slnx`, in the `/samples/` folder:

```xml
<Project Path="samples/Northwind.Server/Northwind.Server.csproj" />
```

- [x] **Step 9: Run the tests**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

Expected: `Passed: 8, Failed: 0, Total: 8`.

- [x] **Step 10: Run the server by hand once**

Run: `dotnet run --project samples/Northwind.Server`

Expected: it starts, prints a listening URL, and creates `northwind.db` next to the binary. Stop it with Ctrl-C. This proves the seeding path outside the test host.

- [x] **Step 11: Commit**

```bash
git add samples/Northwind.Server/ test/InfoCarrier.Core.TransportTests/ InfoCarrier.Core.slnx Directory.Packages.props docs/implementation-plan.md
git commit -m "Step M8-4: the server endpoint, and the first real HTTP hop in this repo

One route, all nine operations. The product's InfoCarrierEnvelopeServer already
checks the protocol version, dispatches and turns a server-side failure into a
fault carried in the response, so the endpoint adds no policy of its own.

Program.cs registers DbContext as well as NorthwindContext, because
InProcessInfoCarrierServer resolves the base type; and it calls
AddInfoCarrierStandardValueMappers, which a server must do for itself (C89).

No sample types in the endpoint file: promoting it to
InfoCarrier.Core.AspNetCore is a file move.

Passed: 8, Failed: 0, Total: 8."
```

- [x] **Fix round 1 (review, Step M8-4a): endpoint error handling, and two disposal leaks.** Code
  review found the endpoint caught nothing: a body that does not deserialize to an
  `InfoCarrierEnvelope`, and `NotSupportedException` from `DispatchAsync`'s protocol-version
  refusal (deliberately outside its own fault-catching `try` — see that method's remarks), both
  fell through to ASP.NET Core's default handling. Under Development that leaked a raw stack
  trace with server file paths; under Production it answered no message at all. The reviewer
  showed `An_unsupported_protocol_version_is_refused_by_number` passing only because the leaked
  Development stack trace happened to contain the string "999" — proved by running the same test
  with `ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production`, where it failed. Fixed
  by catching both paths in the endpoint and answering a deliberate HTTP 400 whose body is only
  the exception's message (no stack trace, no paths); the client's existing non-success path
  turns that into `InfoCarrierTransportException` by design, so the assertion now passes for the
  reason it claims to. `NorthwindServerFactory` pins `builder.UseEnvironment("Production")` so
  the outcome no longer depends on ambient hosting configuration.
  Same review found `NorthwindServerFactory.DisposeAsync` deleting only the main `.db`, leaking
  its WAL-mode `-wal`/`-shm` sidecar files (and occasionally `-journal`) on every run; those three
  paths are now deleted alongside the main file with the same best-effort semantics, and
  `SqliteConnection.ClearAllPools()`'s process-wide scope is now a documented, deliberate choice
  rather than a silent one.
  Passed: 9, Failed: 0, Total: 9 — reconfirmed under
  `ASPNETCORE_ENVIRONMENT=Production DOTNET_ENVIRONMENT=Production`, filtered to
  `An_unsupported_protocol_version_is_refused_by_number` alone: Passed: 1, Failed: 0, Total: 1.

---

### Task 5: A query over HTTP, end to end

This is the task that proves the milestone's premise.

**Files:**
- Create: `test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–4.
- Produces: `NorthwindOverHttpTest`, and a private helper `NorthwindContext CreateClientContext(NorthwindServerFactory factory)` that later tasks reuse.

- [x] **Step 1: Write the failing test**

`test/InfoCarrier.Core.TransportTests/NorthwindOverHttpTest.cs`:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Northwind.Client.Transport;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The premise of the whole product, asserted over a real HTTP hop: a DbContext with no
///     database answers questions about data it cannot reach.
/// </summary>
public class NorthwindOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task A_client_with_no_database_reads_rows_over_http()
    {
        using NorthwindContext context = CreateClientContext(factory);

        List<Customer> customers = await context.Customers
            .Where(c => c.Country == "Germany")
            .OrderBy(c => c.Id)
            .ToListAsync();

        Assert.Equal(["ALFKI"], customers.Select(c => c.Id));
    }

    [Fact]
    public async Task A_projection_crosses_as_columns_rather_than_as_entities()
    {
        using NorthwindContext context = CreateClientContext(factory);

        var rows = await context.Orders
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.CustomerId })
            .ToListAsync();

        Assert.Equal(5, rows.Count);
        Assert.Equal("ALFKI", rows[0].CustomerId);
    }

    [Fact]
    public async Task An_aggregate_is_answered_by_the_server()
    {
        using NorthwindContext context = CreateClientContext(factory);

        int count = await context.OrderDetails.CountAsync(d => d.Quantity >= 10);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Touching_a_navigation_lazy_loads_it_over_a_second_round_trip()
    {
        using NorthwindContext context = CreateClientContext(factory);

        Order order = await context.Orders.SingleAsync(o => o.Id == 1);

        // The query asked for orders and nothing else, so the navigation is empty until it is
        // read. Reading it is what issues the second request.
        Customer? customer = order.Customer;

        Assert.NotNull(customer);
        Assert.Equal("ALFKI", customer.Id);
        Assert.Equal(2, order.OrderDetails.Count);
    }

    internal static NorthwindContext CreateClientContext(NorthwindServerFactory factory)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(factory.CreateClient(), serializer), serializer);

        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)
                .UseLazyLoadingProxies()
                .Options);
    }
}
```

- [x] **Step 2: Run it to verify it fails, then to verify it passes**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --filter "FullyQualifiedName~NorthwindOverHttpTest"`

Expected on the first run: FAIL. **Read the failure before changing anything.** Two are plausible and each means something different:

- *An envelope fails to serialize or deserialize.* `SystemTextJsonInfoCarrierSerializer` uses reflection-based `JsonSerializer` with no `TypeInfoResolver`, which works untrimmed. If this fails, the cause is the shape of `InfoCarrierEnvelope`, not trimming.
- *`Entity type '…' is not in the server model.'`* The two models disagree, which would mean `Northwind.Shared` is not being used by both halves.

If it passes first time, that is a legitimate result — the transport is thin by design.

Expected on the final run: `Passed: 4, Failed: 0` for this filter.

- [x] **Step 3: Run the whole test project**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

Expected: `Passed: 12, Failed: 0, Total: 12`.

**Actual: `Passed: 13, Failed: 0, Total: 13`.** Task 3's review round (Step M8-3a) added a fourth
test to that task after this plan was written, so every downstream running total in this document
is one higher than what is printed here — the arithmetic below (`Task 5 adds 4 (total 12)`) is
stale for the same reason. Not a discrepancy to fix by trimming the suite.

- [x] **Step 4: Commit**

```bash
git add test/InfoCarrier.Core.TransportTests/ docs/implementation-plan.md
git commit -m "Step M8-5: a DbContext with no database answers a query over real HTTP

The premise of the product, asserted across a network boundary for the first
time. Four tests: a filtered entity query, a projection that crosses as columns
rather than as entities (M2, W1), an aggregate answered by the server, and a
navigation lazy-loaded over a second round trip. The last one de-risks phase 2:
automatic lazy loading via UseLazyLoadingProxies is proven over HTTP before a
browser is involved.

Passed: 12, Failed: 0, Total: 12."
```

- [x] **Fix round 1 (review, Step M8-5a): the three value-only assertions.** Code review found
  that three of the four tests asserted the values that came back but not the mechanism their
  names claim, so each would still pass against a product defect it exists to catch: the
  projection test could not tell "projected columns" from "whole entities fetched and projected
  client-side"; the aggregate test could not tell "server-side `COUNT`" from "fetch every matching
  row and count locally"; the lazy-loading test proved the navigations were populated *after*
  being touched but not that touching them was *what* populated them -- an eager over-fetch during
  the first `SingleAsync` would pass every assertion while falsifying the test's own name. Fixed by
  adding `RecordingHandler`, a small reusable `DelegatingHandler` that records request count and
  response bodies (a prototype of the wire-inspector panel a later phase needs), threaded through
  a new `CreateClientContext(factory, out RecordingHandler)` overload -- the original
  `CreateClientContext(factory)` still works, delegating to the new one, so Task 6 is unaffected.
  The projection test now asserts none of the five seeded `OrderDate` values appear in the response
  payload (the projection excludes that column; a whole-entity payload would carry it). The
  aggregate test now asserts exactly one request and a response under 700 bytes (measured: 448
  bytes for the scalar; a row-carrying response measures ~787 bytes/row on the same wire, so three
  `OrderDetail` rows would run well past the bound). The lazy-loading test now asserts the request
  count is 1 right after the initial query, increases after `order.Customer` is read, and increases
  again after `order.OrderDetails` is enumerated. Deliberately broken and un-broken to confirm the
  lazy-loading assertion can fail (asserting the count stayed at 1 after touching `order.Customer`
  failed with `Assert.Equal() Failure: Expected: 1, Actual: 2` against the real, correct
  implementation). Project total unchanged at 13 (no test added, three strengthened).

---

### Task 6: SaveChanges and a transaction over HTTP

**Files:**
- Create: `test/InfoCarrier.Core.TransportTests/NorthwindWritesOverHttpTest.cs`

**Interfaces:**
- Consumes: `NorthwindOverHttpTest.CreateClientContext` from Task 5.
- Produces: nothing later tasks depend on.

- [x] **Step 1: Write the failing test**

`test/InfoCarrier.Core.TransportTests/NorthwindWritesOverHttpTest.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class NorthwindWritesOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task Several_edits_cross_as_one_save()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        List<OrderDetail> lines = await context.OrderDetails
            .Where(d => d.OrderId == 4)
            .OrderBy(d => d.ProductId)
            .ToListAsync();

        Assert.Equal(2, lines.Count);

        lines[0].Quantity += 1;
        lines[1].Quantity += 2;

        int written = await context.SaveChangesAsync();

        Assert.Equal(2, written);

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        List<int> quantities = await verify.OrderDetails
            .Where(d => d.OrderId == 4)
            .OrderBy(d => d.ProductId)
            .Select(d => d.Quantity)
            .ToListAsync();

        Assert.Equal([8, 12], quantities);
    }

    [Fact]
    public async Task An_insert_gets_its_store_generated_key_back()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        var category = new Category { Name = "Seafood" };
        context.Categories.Add(category);

        await context.SaveChangesAsync();

        // The client held a temporary placeholder before the save; the store's own key comes
        // back by correlation id (research-findings 9).
        Assert.True(category.Id > 0);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_the_store_untouched()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        int before = await context.Products.CountAsync();

        using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            context.Products.Add(
                new Product { Name = "Rolled back", UnitPrice = 1.0m, UnitsInStock = 1, CategoryId = 1 });

            await context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        Assert.Equal(before, await verify.Products.CountAsync());
    }

    [Fact]
    public async Task A_committed_transaction_is_visible_to_a_later_context()
    {
        using NorthwindContext context = NorthwindOverHttpTest.CreateClientContext(factory);

        using (IDbContextTransaction transaction = await context.Database.BeginTransactionAsync())
        {
            context.Products.Add(
                new Product { Name = "Committed", UnitPrice = 2.0m, UnitsInStock = 4, CategoryId = 1 });

            await context.SaveChangesAsync();

            await transaction.CommitAsync();
        }

        using NorthwindContext verify = NorthwindOverHttpTest.CreateClientContext(factory);
        Assert.True(await verify.Products.AnyAsync(p => p.Name == "Committed"));
    }
}
```

- [x] **Step 2: Run it**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj --filter "FullyQualifiedName~NorthwindWritesOverHttpTest"`

Expected: `Passed: 4, Failed: 0`.

**Actual: `Passed: 4, Failed: 0` on the first run** — no transaction-token failure, so `Program.cs`'s
`AddSingleton<IInfoCarrierServer, …>` registration (already in place from M8-4) needed no change.

**If the transaction tests fail with "transaction not found":** the transaction token (W3) keys a server-side context that must survive between requests. `InProcessInfoCarrierServer` holds it in a `ConcurrentDictionary`, so `IInfoCarrierServer` must be a **singleton** — check that `Program.cs` used `AddSingleton` and not `AddScoped`.

- [x] **Step 3: Run the whole test project**

Run: `dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj`

Expected: `Passed: 16, Failed: 0, Total: 16`.

**Actual: `Passed: 17, Failed: 0, Total: 17`.** As with Task 5's Step 3, the running total in this
document is stale by one: M8-3a's review round added a fourth test to Task 3 after this plan's
arithmetic was written, so every downstream total here undercounts by one. Not a discrepancy to
fix by trimming the suite.

- [x] **Step 4: Commit**

```bash
git add test/InfoCarrier.Core.TransportTests/ docs/implementation-plan.md
git commit -m "Step M8-6: SaveChanges and transactions over real HTTP

Four tests: several edits crossing as one save (the unit-of-work shape), an
insert getting its store-generated key back by correlation id, a rolled-back
transaction leaving the store untouched, and a committed one visible to a later
context. The transaction pair is what proves the W3 token survives across
separate HTTP requests.

Passed: 16, Failed: 0, Total: 16."
```

---

### Task 7: CI, and the honest record of what Phase 1 did not close

**Files:**
- Modify: `.github/workflows/build.yml`
- Modify: `CLAUDE.md`
- Modify: `docs/roadmap.md`
- Modify: `docs/implementation-plan.md`

**Interfaces:**
- Consumes: everything.
- Produces: nothing.

- [ ] **Step 1: Add the transport tests to the fast gate**

In `.github/workflows/build.yml`, in the `fast-gate` job, after the existing `Test (round-trip + smoke)` step:

```yaml
      # Phase 1 of M8. A separate project on purpose: InfoCarrier.Core.FunctionalTests is what
      # the spec ratchet counts, and these are not spec tests.
      - name: Test (HTTP transport)
        run: >
          dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj
          --no-build --configuration Release
```

- [ ] **Step 2: Correct the two stale roadmap items**

In `docs/roadmap.md`:

- Replace the `Measured 2026-08-10 (artifacts/measure/c54): Total tests: 22355, Passed: 22006, Failed: 132, Skipped: 217` paragraph and the sentence beginning `The 132 are classified` with the current figure: `Measured 2026-08-11 (artifacts/measure/c96): Total tests: 22453, Passed: 22219, Failed: 13, Skipped: 221. All 13 are classified in the archived plan's C96; ten are permanent by design or upstream.`
- Delete the `**Known defects to fix in M1:** build.yml restores InfoCarrier.Core.sln …` paragraph in §CI strategy. It described the workflow before `51f4684`; C39 fixed it on 2026-08-10 and CI has been correct since.

- [ ] **Step 3: Record what Phase 1 did *not* close**

Append to `docs/implementation-plan.md` under Phase H:

```markdown
**Three things Phase 1 leaves open, stated so Phase 2 does not rediscover them.**

- **`SystemTextJsonInfoCarrierSerializer` uses reflection-based `JsonSerializer`.** Its
  `JsonSerializerOptions` sets no `TypeInfoResolver`, so the envelope and the request/response
  records are serialized reflectively. That is fine untrimmed and **will fail in a trimmed Blazor
  WASM build**, where the SDK sets `JsonSerializerIsReflectionEnabledByDefault=false`. Note that
  the *expression tree* is already safe — it goes through the source-generated
  `ExpressionJsonContext`. So the fix is bounded: a source-generated context for the envelope and
  the nine operation payloads. **This is Phase 2's first task.**
- **The response direction is bounded by `MaxRequestBytes`.** `InfoCarrierEnvelope` implements
  `IInfoCarrierRequest`, and `InfoCarrierPayloadLimits.Guard<T>` picks its bound from that
  interface — so a client deserializing a *response* envelope applies the 64 MiB **request**
  bound. The envelope's own doc comment already says the two legs are not distinguished and that
  fixing it is part of M5's envelope criterion. Harmless for Northwind; a large result would fail
  confusingly.
- **M8's HTTP transport criterion is formally still open**, because the two transport files live
  in `samples/` rather than in packages. Both are free of sample types, so the promotion is a
  file move; see the spec §4.1.
```

- [ ] **Step 4: Update CLAUDE.md's Current state**

Add to the `Not yet implemented` list, as the first bullet:

```markdown
- **The HTTP transport works and is tested (M8 Phase 1).** A `DbContext` with no database answers
  queries, saves and runs transactions against a SQLite-backed ASP.NET Core server over a real
  HTTP hop — `test/InfoCarrier.Core.TransportTests`, 16 of 16. **That project is deliberately not
  `InfoCarrier.Core.FunctionalTests`**: the ratchet counts the latter and its number must keep
  meaning "inherited spec tests failing". `eng/measure.sh` was scoped to one project in the same
  phase, because it parses the *last* `Total tests:` block and a second test project in the
  solution would have silently corrupted every measurement.
```

- [ ] **Step 5: Verify the whole build and both suites**

Run:

```bash
dotnet build InfoCarrier.Core.slnx
dotnet test test/InfoCarrier.Core.TransportTests/InfoCarrier.Core.TransportTests.csproj
bash eng/measure.sh m8-phase1 c96
```

Expected: build succeeds; `Passed: 16, Failed: 0, Total: 16`; and `FAILING: 13  TOTAL: 22453` with empty FIXED, BROKEN and REASONS diffs.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/build.yml CLAUDE.md docs/roadmap.md docs/implementation-plan.md
git commit -m "Step M8-7: wire the transport tests into CI, and record what Phase 1 left open

The fast gate runs the new project as a second step. It stays out of the spec
ratchet, whose number means 'inherited spec tests failing'.

Two stale roadmap items corrected: the 'where we are' figures were c54/132 and
are now c96/13, and the 'Known defects to fix in M1' note about build.yml
restoring a .sln described the workflow before 51f4684 -- C39 fixed it on
2026-08-10.

Three open items recorded so Phase 2 does not rediscover them: the serializer is
reflection-based and will fail trimmed (bounded fix -- the expression tree
already uses a source-generated context); the response leg is bounded by
MaxRequestBytes because the envelope is typed as a request; and M8's transport
criterion stays formally open until the two files are promoted to packages.

Spec suite unchanged at 13/22453."
```

---

## Self-Review

**Spec coverage.** §3 projects → Tasks 2, 3, 4. §3.1 shared context → Task 2. §3.2 automatic lazy loading → Task 2 Step 3 (model) and Tasks 2/4/5 (options on both halves), proved over HTTP by Task 5's fourth test. §3.3 shared configuration / D2 → Task 2, and its commit message records the size evidence D2 asks for. §3.4 server hosts client and seeds → Task 4 Steps 6, 10. §4 transport → Tasks 3, 4. §4.1 promotion constraint → Task 3 Step 8 (checked, not merely asserted). §4.2 D1 → carried; Task 7 Step 3 records the related `MaxRequestBytes` consequence. §6 error handling → Task 3 Step 1 (transport failures) and Task 6 (rollback). §8 tests and CI → Tasks 5, 6, 7. §10 phase split → this plan is Phase 1 only.

**Not covered here, by design:** §5 pages, §7 trimming, and the compiled model are Phase 2. §9's five success criteria span both phases; Phase 1 satisfies criterion 3 and half of criterion 5.

**Type consistency.** `CreateClientContext` is defined once (Task 5) and reused by name in Task 6. `NorthwindServerFactory` is defined in Task 4 and used in Tasks 4, 5, 6. `HttpInfoCarrierTransport`'s three-argument constructor is used with two arguments throughout, relying on the `requestUri` default. `InfoCarrierTransportException` is thrown in Task 3 and asserted in Tasks 3 and 4.

**Test count arithmetic:** Task 2 adds 3 (total 3), Task 3 adds 3 (total 6), Task 4 adds 2 (total 8), Task 5 adds 4 (total 12), Task 6 adds 4 (total 16).
