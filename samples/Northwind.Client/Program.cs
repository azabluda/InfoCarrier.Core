// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Northwind.Client;
using Northwind.Client.Transport;
using Northwind.Client.Wire;
using Northwind.Shared;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentUIComponents();
builder.Services.AddSingleton<WireLog>();

// ---------------------------------------------------------------------------------------------
// This is the whole client wiring, and it is the point of the sample. Nothing below configures a
// database, because the browser has none. Everything singleton: WebAssembly is one user in one
// tab, so a scope would be ceremony without a lifetime behind it.
// ---------------------------------------------------------------------------------------------

// Same origin as the page. The server hosts these files (spec §3.4), so there is one URL, one
// launch profile and no CORS.
builder.Services.AddSingleton(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<IInfoCarrierSerializer, SystemTextJsonInfoCarrierSerializer>();

builder.Services.AddSingleton<IInfoCarrierClient>(services =>
{
    var serializer = services.GetRequiredService<IInfoCarrierSerializer>();

    IInfoCarrierTransport transport = new HttpInfoCarrierTransport(
        services.GetRequiredService<HttpClient>(), serializer);

    // The inspector rides on the seam rather than inside the transport, which is what keeps
    // HttpInfoCarrierTransport free of sample types and therefore promotable by a file move.
    transport = new InspectingTransport(transport, serializer, services.GetRequiredService<WireLog>());

    return new TransportInfoCarrierClient(transport, serializer);
});

// A factory, not a context: each page owns its own unit of work, which is exactly what the Order
// page is there to show. A DbContext held for the lifetime of the app would accumulate tracked
// entities across pages and make "one SaveChanges for several edits" mean nothing.
builder.Services.AddDbContextFactory<NorthwindContext>((services, options) => options
    .UseInfoCarrier(services.GetRequiredService<IInfoCarrierClient>())

    // Automatic lazy loading (spec §3.2). Castle DynamicProxy needs Reflection.Emit, which the
    // Mono *interpreter* provides and an AOT-compiled build does not -- and a Blazor release
    // publish trims without AOT, so this is expected to survive M8-17's trimmed gate. It is an
    // experiment with a known fallback (ILazyLoader injection, confined to Northwind.Shared/Model),
    // not an assumption.
    .UseLazyLoadingProxies());

await builder.Build().RunAsync();
