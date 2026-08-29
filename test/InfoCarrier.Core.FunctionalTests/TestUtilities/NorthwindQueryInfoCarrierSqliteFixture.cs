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
public class NorthwindQueryInfoCarrierSqliteFixture<TModelCustomizer>
    : NorthwindQueryFixtureBase<TModelCustomizer>, ITestSqlLoggerFactory
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <summary>
    ///     Gets the SQL logger factory the relational query asserter reaches for.
    /// </summary>
    /// <remarks>
    ///     <c>RelationalQueryAsserter</c> casts the fixture to <see cref="ITestSqlLoggerFactory" />
    ///     and calls <c>OutputSql()</c> on the failure path only. Without the interface a failing
    ///     assertion would surface as an <see cref="InvalidCastException" /> and hide its own
    ///     reason. Nothing new is constructed: <c>InfoCarrierTestStoreFactory</c> already builds a
    ///     <see cref="TestSqlLoggerFactory" /> rather than a bare <c>ListLoggerFactory</c>.
    /// </remarks>
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server),
            serverContextType: typeof(NorthwindInfoCarrierSqliteServerContext),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    protected override bool ShouldLogCategory(string logCategory)
        => logCategory == DbLoggerCategory.Query.Name;

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
