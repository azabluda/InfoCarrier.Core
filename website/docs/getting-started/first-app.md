# Your first client and server

A working pair, start to finish. Every snippet on this page is taken from a project that compiles
and runs.

## 1. The shared model

Put the entity classes and the `DbContext` in a project both halves reference. This is what makes
the wire format work: the payload names entity types and properties, the two ends resolve those
names against their own models, and sharing the source makes the two models agree by
construction.

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

The constructor takes the non-generic `DbContextOptions` on purpose. The client builds its options
with `UseInfoCarrier` and the server with `UseSqlServer`, and the non-generic form accepts both, so
one context class serves both halves.

## 2. The server

An ordinary ASP.NET Core application with an ordinary EF Core provider, one role registration, and
one endpoint.

```csharp title="Shop.Server/Program.cs"
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Shop;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("Shop")!;

// Your real database. Nothing about this line is InfoCarrier's business.
builder.Services.AddDbContext<ShopContext>(o => o.UseSqlServer(connectionString));

builder.Services.AddInfoCarrierServer<ShopContext>();

WebApplication app = builder.Build();

// One endpoint. It defaults to the route "infocarrier"; the client defaults to the same.
app.MapInfoCarrier();

app.Run();
```

`AddInfoCarrierServer<ShopContext>` registers the default serializer and in-process server. It
resolves `ShopContext` directly, so no base `DbContext` alias is required.

## 3. The client

Four lines of wiring, and then it is EF Core.

```csharp title="Shop.Client/Program.cs"
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Shop;

using var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, options =>
    options.WithEndpoint("https://your-app-server/infocarrier"));

DbContextOptions options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client)
    .Options;
```

`InfoCarrierClient.Create` assembles the default serializer, HTTP transport, and client. The caller
owns the supplied `HttpClient`. These low-level parts remain replaceable:

| | |
|---|---|
| `SystemTextJsonInfoCarrierSerializer` | turns envelopes into bytes. Source-generated, so it works in a trimmed build. |
| `HttpInfoCarrierTransport` | posts the bytes and reads the answer. One method: see [Custom transports](../configuration/transports.md). |
| `TransportInfoCarrierClient` | the client the provider talks to. |

In a dependency injection application, register the complete client role instead:

```csharp
builder.Services.AddInfoCarrierClient(options =>
    options.WithEndpoint("https://your-app-server/infocarrier"));

builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());
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

- [Querying](../guide/querying.md): what runs where, and how to tell.
- [Saving changes](../guide/saving-changes.md): units of work, graphs, generated keys.
- [Transactions](../guide/transactions.md): including one transaction across two contexts.
- [Handling errors](../guide/errors.md): what a failed server and a failed network look like, and
  why they are different exception types.

## Testing without a network

`IInfoCarrierTransport` has one method, so a test can send the request straight to the server. This
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
var services = new ServiceCollection()
    .AddLogging()
    .AddDbContext<ShopContext>(o => o.UseSqlite("Filename=test.db"));
services.AddInfoCarrierServer<ShopContext>();
ServiceProvider serverServices = services.BuildServiceProvider();

var serializer = new SystemTextJsonInfoCarrierSerializer();

IInfoCarrierClient client = new TransportInfoCarrierClient(
    new LoopbackTransport(serverServices.GetRequiredService<IInfoCarrierServer>(), serializer),
    serializer);
```
