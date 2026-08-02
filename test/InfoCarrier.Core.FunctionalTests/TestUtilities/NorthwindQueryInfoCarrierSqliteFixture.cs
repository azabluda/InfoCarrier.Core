// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The Northwind query fixture on ADR-009 Tier B — the client remotes to a server running
///     against SQLite rather than InMemory.
/// </summary>
/// <remarks>
///     Tier A's provider client-evaluates nearly everything, so a query failing there may be
///     failing because InMemory cannot do it rather than because this provider is wrong. Running
///     the same inherited bases against a backend that genuinely translates is what separates
///     the two, and it is what turns the InMemory-limitation overrides from an assumption into a
///     measurement (roadmap M3).
/// </remarks>
/// <typeparam name="TModelCustomizer">The model customizer.</typeparam>
public class NorthwindQueryInfoCarrierSqliteFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server),
            serverContextType: typeof(NorthwindInfoCarrierSqliteServerContext));

    /// <inheritdoc />
    protected override bool ShouldLogCategory(string logCategory)
        => logCategory == DbLoggerCategory.Query.Name;

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
