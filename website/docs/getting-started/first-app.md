# Your first client and server

A working pair, start to finish. Every snippet on this page is taken from a project that compiles
and runs.

## 1. The shared model

Put the entity classes and the `DbContext` in a project both halves reference. This is not a
convention — it is what makes the wire format work. The payload names entity types and properties,
and the two ends resolve those names against **their own** models, so the models must agree.
Sharing the source makes that true by construction.

```csharp title="Shop.Shared/Model.cs"
using Microsoft.EntityFrameworkCore;

namespace Shop;

public class Customer
{
    public string Id { get; set; } = "";
    public string Company { get; set; } = "";
    public string Country { get; set; } = "";
    public List<Order> Orders { get; set; } = [];
}

public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; } = "";
    public Customer? Customer { get; set; }
    public DateTime PlacedOn { get; set; }
    public decimal Freight { get; set; }
}

public class ShopContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasKey(c => c.Id);
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<Order>()
            .HasOne(o => o.Customer).WithMany(c => c.Orders)
            .HasForeignKey(o => o.CustomerId);
    }
}
```

!!! note "Take `DbContextOptions`, not `DbContextOptions<ShopContext>`"

    The client builds its options with `UseInfoCarrier` and the server with `UseSqlServer` (or
    whatever it uses). A constructor taking the non-generic `DbContextOptions` accepts both, so one
    context class serves both halves.

## 2. The server

An ordinary ASP.NET Core application with an ordinary EF Core provider, plus four registrations and
one endpoint.

```csharp title="Shop.Server/Program.cs"
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Shop;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Your real database. Nothing about this line is InfoCarrier's business.
builder.Services.AddDbContext<ShopContext>(o => o.UseSqlServer(connectionString));

// The server resolves the context per request as `DbContext`, so the base type must resolve too.
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>());

builder.Services
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers();

WebApplication app = builder.Build();

// One endpoint. It defaults to the route "infocarrier"; the client defaults to the same.
app.MapInfoCarrier();

app.Run();
```

`InProcessInfoCarrierServer` is the piece that executes a request against a real `DbContext`. The
name says *in-process* because it runs the query in the same process as the database connection —
it is the normal server-side implementation, not a test double.

## 3. The client

Four lines of wiring, and then it is EF Core.

```csharp title="Shop.Client/Program.cs"
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Shop;

var serializer = new SystemTextJsonInfoCarrierSerializer();
using var http = new HttpClient { BaseAddress = new Uri("https://your-app-server") };

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new HttpInfoCarrierTransport(http, serializer),
    serializer);

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client)
    .Options;
```

Three objects, each replaceable:

| | |
|---|---|
| `SystemTextJsonInfoCarrierSerializer` | turns envelopes into bytes. Source-generated, so it works in a trimmed build. |
| `HttpInfoCarrierTransport` | posts the bytes and reads the answer. One method — see [Custom transports](../configuration/transports.md). |
| `TransportInfoCarrierClient` | the client the provider talks to. |

In a DI application, register them instead:

```csharp
builder.Services.AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>();
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri("https://your-app-server") });

builder.Services.AddSingleton<IInfoCarrierClient>(sp =>
{
    var serializer = sp.GetRequiredService<IInfoCarrierSerializer>();
    return new TransportInfoCarrierClient(
        new HttpInfoCarrierTransport(sp.GetRequiredService<HttpClient>(), serializer),
        serializer);
});

builder.Services.AddDbContextFactory<ShopContext>((sp, o) =>
    o.UseInfoCarrier(sp.GetRequiredService<IInfoCarrierClient>()));
```

!!! tip "A factory, not a context"

    `AddDbContextFactory` gives each screen or operation its own change tracker. A single
    long-lived client `DbContext` accumulates tracked entities for the life of the application,
    which makes "several edits, one `SaveChanges`" mean nothing.

## 4. Use it

From here there is no InfoCarrier API to learn. It is EF Core:

```csharp
await using var context = new ShopContext(options);

List<Order> recent = await context.Orders
    .Include(o => o.Customer)
    .Where(o => o.Customer!.Country == "Germany" && o.Freight > 50m)
    .OrderByDescending(o => o.PlacedOn)
    .Take(20)
    .ToListAsync();

recent[0].Freight = 0m;
context.Orders.Add(new Order { CustomerId = "AROUT", PlacedOn = DateTime.UtcNow, Freight = 5m });

await context.SaveChangesAsync();   // one round trip, two rows
```

Read on:

- [Querying](../guide/querying.md) — what runs where, and how to tell.
- [Saving changes](../guide/saving-changes.md) — units of work, graphs, generated keys.
- [Transactions](../guide/transactions.md) — including one transaction across two contexts.

## Testing without a network

`IInfoCarrierTransport` is one method, so a test can hand the request straight to the server. This
transport serializes both ways, so nothing travels by reference that would not survive HTTP:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.Common;

public sealed class LoopbackTransport(IInfoCarrierServer server, IInfoCarrierSerializer serializer)
    : IInfoCarrierTransport
{
    private readonly InfoCarrierEnvelopeServer _endpoint = new(server, serializer);

    public async Task<InfoCarrierEnvelope> SendAsync(
        InfoCarrierEnvelope request,
        CancellationToken cancellationToken = default)
    {
        byte[] payload = serializer.Serialize(request);
        InfoCarrierEnvelope onTheWire = serializer.Deserialize<InfoCarrierEnvelope>(payload)!;

        InfoCarrierEnvelope response = await _endpoint.DispatchAsync(onTheWire, cancellationToken);

        return serializer.Deserialize<InfoCarrierEnvelope>(serializer.Serialize(response))!;
    }
}
```

Wire it exactly as the HTTP one, with a server built over an in-memory or file-backed store:

```csharp
ServiceProvider serverServices = new ServiceCollection()
    .AddDbContext<ShopContext>(o => o.UseSqlite("Filename=test.db"))
    .AddScoped<DbContext>(sp => sp.GetRequiredService<ShopContext>())
    .AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>()
    .AddSingleton<IInfoCarrierServer, InProcessInfoCarrierServer>()
    .AddInfoCarrierStandardValueMappers()
    .BuildServiceProvider();

var serializer = new SystemTextJsonInfoCarrierSerializer();

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new LoopbackTransport(serverServices.GetRequiredService<IInfoCarrierServer>(), serializer),
    serializer);
```
