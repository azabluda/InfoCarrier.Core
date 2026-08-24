// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.FluentUI.AspNetCore.Components;
using Northwind.Client;
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
//
// IL2026 IS ACKNOWLEDGED HERE, NOT SILENCED IN THE PROJECT FILE, AND THE DIFFERENCE IS THE POINT.
// `UseInfoCarrier` carries [RequiresUnreferencedCode] and [RequiresDynamicCode] because this
// provider resolves types by the name on the wire and builds expression trees at run time. That is
// the provider telling a trimmed consumer the truth, and every consumer of a trimmed client meets
// this warning at their own call site. THIS SAMPLE IS WHAT THEY DO ABOUT IT: acknowledge it where
// the decision is made, having tested the paths this model uses -- which M8-17 did, driving all
// three pages against the published trimmed output.
//
// A `WarningsNotAsErrors` entry beside IL2110/IL2111 would be wrong. Those two are the framework's
// own Razor output, nothing here can fix them, and they are not about this call. This one is ours,
// it is about this call, and hiding it project-wide would also hide the next one.
#pragma warning disable IL2026 // UseInfoCarrier is [RequiresUnreferencedCode]; see above.
builder.Services.AddDbContextFactory<NorthwindContext>((services, options) => options
    .UseInfoCarrier(services.GetRequiredService<IInfoCarrierClient>())

    // NOTE THE ABSENCE: there is no UseLazyLoadingProxies() here, and the server does call it.
    //
    // Automatic lazy loading cannot work in a browser. A navigation property getter is
    // SYNCHRONOUS, so a lazy load must block on the HTTP round trip, and a single-threaded
    // WebAssembly runtime cannot block: reading `order.Customer` throws
    // `PlatformNotSupportedException: Cannot wait on monitors on this runtime` -- and throws AFTER
    // the request has already gone out, so the panel shows the round trip while the value never
    // arrives. `ILazyLoader.Load()` is synchronous too, so injecting a loader instead fails
    // identically. This is the browser's constraint, not this provider's: a console or desktop
    // client of InfoCarrier lazy-loads normally, and samples/Northwind.Demo does.
    //
    // It is left OFF rather than configured-but-unusable so that an unloaded navigation is simply
    // `null`, which is honest and debuggable, instead of a confusing exception from deep inside a
    // proxy. The Order page loads navigations with `LoadAsync`, which is the supported shape here.
    //
    // The asymmetry with the server is safe, and it was checked rather than assumed: all three
    // pages run against a server that DOES enable proxies. What proxies change is how an entity is
    // constructed on the side that enables them, plus a `LazyLoader` service property on that
    // side's model -- neither is something the wire names, and A49 is about names.
    );
#pragma warning restore IL2026

await builder.Build().RunAsync();
