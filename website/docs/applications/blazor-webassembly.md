# Build a Blazor WebAssembly client

Add a browser client to the Shop server from the [first tutorial](../getting-started/first-app.md).
This page creates a customer list at `http://localhost:5182/customers`.

Keep the tutorial server running at `http://localhost:5180`.
Run the commands below from the `ShopTutorial` directory.

## 1. Create the browser project

```powershell
dotnet new blazorwasm -n Shop.Browser -f net11.0
dotnet add Shop.Browser reference Shop.Shared
dotnet add Shop.Browser package InfoCarrier.Core
```

The browser references Core and your shared model.
It does not need the server endpoint package or the SQLite provider.

## 2. Register the client and context factory

Replace `Shop.Browser/Program.cs`:

```csharp
using InfoCarrier.Core;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Shop;
using Shop.Browser;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddInfoCarrierClient(o =>
    o.WithEndpoint("http://localhost:5180/infocarrier"));
builder.Services.AddDbContextFactory<ShopContext>(o => o.UseInfoCarrier());

await builder.Build().RunAsync();
```

A context factory gives each operation its own context.
Components can use it without sharing a change tracker across the whole browser tab.

## 3. Allow the local browser origin

The browser and server use different ports in this example.
Add this before `builder.Build()` in `Shop.Server/Program.cs`:

```csharp
builder.Services.AddCors(options => options.AddPolicy("ShopBrowser", policy =>
    policy.WithOrigins("http://localhost:5182")
        .AllowAnyHeader()
        .AllowAnyMethod()));
```

After `builder.Build()`, add:

```csharp
app.UseCors();
```

Replace the existing endpoint mapping with:

```csharp
app.MapInfoCarrier().RequireCors("ShopBrowser");
```

Restart the server.
This cross-origin policy allows the local browser origin; it does not authenticate users.
For deployment, configure [authentication and authorization](../operations/security.md)
and use HTTPS on both applications.

When the server hosts the browser files at the same origin, this separate cross-origin policy is unnecessary.

## 4. Add the customer page

Create `Shop.Browser/Pages/Customers.razor`:

```razor
@page "/customers"
@using Microsoft.EntityFrameworkCore
@using Shop
@inject IDbContextFactory<ShopContext> Contexts

<h1>Customers</h1>

@if (customers is null)
{
    <p>Loading customers...</p>
}
else
{
    <ul>
        @foreach (Customer customer in customers)
        {
            <li>@customer.Company (@customer.Country)</li>
        }
    </ul>
}

@code {
    private List<Customer>? customers;

    protected override async Task OnInitializedAsync()
    {
        await using var context = await Contexts.CreateDbContextAsync();
        customers = await context.Customers.AsNoTracking()
            .OrderBy(c => c.Company).ThenBy(c => c.Id)
            .Take(20)
            .ToListAsync();
    }
}
```

The component loads data asynchronously and disposes its context when the query finishes.

## 5. Open it

In another terminal:

```powershell
dotnet run --project Shop.Browser --no-launch-profile -- --urls http://localhost:5182
```

Open <http://localhost:5182/customers>.
The list shows the customer from the tutorial.
If you ran its save example, the company is `Updated Shop`; otherwise it is `Example Shop`.

## Add more application behavior

Use the same asynchronous [query](querying.md), [save](saving.md), and
[transaction](transactions.md) methods as the console client.
A data grid should create a context for each request and apply ordering before paging.

Load relationships with `Include` or `LoadAsync`.
For example:

```csharp
await context.Entry(customer).Collection(c => c.Orders).LoadAsync();
```

Use asynchronous calls in the browser. Automatic lazy-loading proxies require synchronous HTTP,
so use [explicit relationship loading](related-data.md) instead.

## Publish the browser client

```powershell
dotnet publish Shop.Browser -c Release
```

Serve the published static files from your web host.
Set the client endpoint to the deployed server address.
Test the application's queries, saves, and relationships in the published output.

Trimming can remove code reached through reflection.
The library reports trimming warnings because the trimmer cannot prove every application's model is preserved.
The Northwind browser sample has been tested after trimming; that does not establish the result
for a different application model. Verify your own paths before deployment.

## Optional: compiled models

The normal model built at startup is sufficient for this example.
A compiled model is a separate optimization; it is not required for a browser client.

If you generate one, ensure it was generated for the client configuration rather than the server's
database provider and proxy settings.
Entity Framework Core 11's compiled-model initialization may use a thread unavailable in the browser.
Its inline-initialization switch is:

```csharp
AppContext.SetSwitch("Microsoft.EntityFrameworkCore.Issue31751", true);
```

See [the upstream issue](https://github.com/dotnet/efcore/issues/31751).
Test the generated model in the actual browser build before choosing that optimization.

## Feedback

For questions or problems, [open an issue](https://github.com/azabluda/InfoCarrier.Core/issues/new).
