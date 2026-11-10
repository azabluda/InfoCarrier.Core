# Build your first client and server

This tutorial creates a console client, an ASP.NET Core server, and a shared model.
The server creates a SQLite database and seeds one customer. The client reads that customer,
updates it, and reads it again through a fresh context.

These instructions use the version 11 development source described in [installation](installation.md).
They are for local development. Secure a deployed endpoint using the [security guide](../operations/security.md).

## Before you start

Follow [installation](installation.md) to install the SDK and clone the source.
The commands below use PowerShell and start in that checkout.

| Project | Job |
|---|---|
| `Shop.Shared` | Entity classes and the context model used by both applications |
| `Shop.Server` | SQLite database and HTTP endpoint |
| `Shop.Client` | Console application that reads and saves through the server |

## 1. Create three projects

Run these commands in PowerShell. Start in your InfoCarrier repository checkout:

```powershell
$iccSource = (Get-Location).Path
Set-Location ..
New-Item -ItemType Directory -Name ShopTutorial
Set-Location ShopTutorial

dotnet new classlib -n Shop.Shared -f net10.0
dotnet new web -n Shop.Server -f net10.0
dotnet new console -n Shop.Client -f net10.0

dotnet add Shop.Shared reference "$iccSource/src/InfoCarrier.Core/InfoCarrier.Core.csproj"
dotnet add Shop.Server reference Shop.Shared "$iccSource/src/InfoCarrier.Core.AspNetCore/InfoCarrier.Core.AspNetCore.csproj"
dotnet add Shop.Client reference Shop.Shared
dotnet add Shop.Server package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.1
```

The provider version is pinned here to match the current development dependency.
Delete `Shop.Shared/Class1.cs`. Keep this terminal in `ShopTutorial`.

## 2. Add the shared model

Create `Shop.Shared/Model.cs`:

```csharp
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

Both applications use this model. The server selects SQLite; the client selects InfoCarrier.

## 3. Create and seed the server database

Replace `Shop.Server/Program.cs`:

```csharp
using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Shop;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ShopContext>(o => o.UseSqlite("Data Source=shop.db"));
builder.Services.AddInfoCarrierServer<ShopContext>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Customers.AnyAsync())
    {
        db.Customers.Add(new Customer
        {
            Id = "ALFKI", Company = "Example Shop", Country = "Germany",
            Orders = [new Order { PlacedOn = new DateTime(2026, 1, 1), Freight = 5m }]
        });
        await db.SaveChangesAsync();
    }
}

app.MapInfoCarrier();
await app.RunAsync();
```

This creates the database through the server's SQLite context and adds the example data once.

Start the server:

```powershell
dotnet run --project Shop.Server --no-launch-profile -- --urls http://localhost:5180
```

Wait for `Now listening on: http://localhost:5180`.
The database file is `shop.db` in the server's working directory.

## 4. Read and save from the client

Replace `Shop.Client/Program.cs`:

```csharp
using InfoCarrier.Core;
using Microsoft.EntityFrameworkCore;
using Shop;

using var http = new HttpClient();
IInfoCarrierClient client = InfoCarrierClient.Create(http, o =>
    o.WithEndpoint("http://localhost:5180/infocarrier"));

var options = new DbContextOptionsBuilder<ShopContext>()
    .UseInfoCarrier(client).Options;

await using (var db = new ShopContext(options))
{
    var customer = await db.Customers.SingleAsync(c => c.Id == "ALFKI");
    Console.WriteLine($"Before: {customer.Company}");
    customer.Company = "Updated Shop";
    await db.SaveChangesAsync();
}

await using (var db = new ShopContext(options))
{
    var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == "ALFKI");
    Console.WriteLine($"After: {customer.Company}");
}
```

In another PowerShell terminal, go to `ShopTutorial` and run:

```powershell
dotnet run --project Shop.Client
```

On the first run, the output is:

```text
Before: Example Shop
After: Updated Shop
```

Later runs start with `Before: Updated Shop`. The database keeps your changes.
The second context confirms the saved value came back from the server.

## Continue

- [Query data](../applications/querying.md): filter, select, and page results.
- [Save changes](../applications/saving.md): insert, update, and delete entities.
- [Load relationships](../applications/related-data.md) or [use transactions](../applications/transactions.md).
- [Configure a client](../applications/configuration/client.md) when adding a context factory to an application.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
