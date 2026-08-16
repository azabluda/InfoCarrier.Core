// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Northwind.Shared;

namespace Northwind.Client;

/// <summary>
///     Lets <c>dotnet ef dbcontext optimize</c> build this client's model without a browser and
///     without a server.
/// </summary>
/// <remarks>
///     <para>
///         The compiled model has to be generated against the <b>client's</b> configuration rather
///         than the server's, because it is the client that will use it: the two halves build their
///         models with different providers, and only this one is an InfoCarrier model. That is
///         possible at all because C90 gave this provider design-time services.
///     </para>
///     <para>
///         The transport below refuses every call, which is correct rather than a shortcut:
///         building a model reads the CLR types and <c>OnModelCreating</c>, and never asks a server
///         anything. A transport that quietly returned nothing would hide a real mistake if that
///         ever stopped being true.
///     </para>
/// </remarks>
public sealed class NorthwindDesignTimeFactory : IDesignTimeDbContextFactory<NorthwindContext>
{
    /// <inheritdoc />
    public NorthwindContext CreateDbContext(string[] args)
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(new UnreachableTransport(), serializer);

        return new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(client)

                // Configured here for the same reason Program.cs configures it: proxies add a
                // model convention, and a compiled model that did not have it would not be the
                // model the app runs with.
                .UseLazyLoadingProxies()
                .Options);
    }

    private sealed class UnreachableTransport : IInfoCarrierTransport
    {
        public Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Building a compiled model does not contact a server. Reaching this means "
                + "something asked the store a question while the model was being built.");
    }
}
