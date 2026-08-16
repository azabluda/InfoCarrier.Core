// Licensed under the MIT license. See license.txt file in the project root for license information.

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

// Serve the Blazor client from this same host (spec §3.4). One origin removes CORS, removes a
// second launch profile, and makes `dotnet run --project samples/Northwind.Server` the whole
// story. MapFallbackToFile is what lets the client's own router own every path that is not the
// InfoCarrier endpoint or a static file.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapInfoCarrier();

app.MapFallbackToFile("index.html");

app.Run();

/// <summary>
///     Named so that <c>WebApplicationFactory&lt;Program&gt;</c> can find an entry point. A
///     top-level-statements program has an internal one otherwise.
/// </summary>
public partial class Program;
