// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The InfoCarrier Northwind query fixture (v1's <c>NorthwindQueryInfoCarrierFixture</c>,
///     rebuilt for EF Core 10). Overrides only <see cref="TestStoreFactory" /> — the client
///     context remotes through the InfoCarrier backend store (InMemory first).
/// </summary>
/// <typeparam name="TModelCustomizer">The model customizer.</typeparam>
public class NorthwindQueryInfoCarrierFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server));

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
