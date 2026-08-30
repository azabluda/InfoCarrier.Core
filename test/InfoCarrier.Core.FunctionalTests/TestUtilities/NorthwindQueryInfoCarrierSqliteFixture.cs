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

    /// <summary>
    ///     Snaps <c>OrderDetail.Discount</c> back to its two-decimal value after seeding.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>Discount</c> is a <see cref="float" />. <c>NorthwindData</c>'s <c>0.15f</c> lands
    ///         in SQLite's 8-byte <c>REAL</c> column as <c>0.150000005960464…</c> — the 32-bit
    ///         value shown at 64-bit width — where EF Core's own SQLite suite reads a clean
    ///         <c>0.15</c> because it uses a prebuilt <c>northwind.db</c> rather than seeding from
    ///         the model (this tier has to build its store from the model; see
    ///         <see cref="NorthwindInfoCarrierSqliteServerContext" />).
    ///     </para>
    ///     <para>
    ///         That widening gives <c>ef_sum(CAST("Discount" AS TEXT))</c> a per-row residual, and
    ///         <c>NorthwindAggregateOperatorsQueryInfoCarrierTest.Type_casting_inside_sum</c>
    ///         (sync + async) sums the whole table, so it differs from EF's expected <c>121.040</c>
    ///         by about 1.8e-6. <c>round(x, 2)</c> over every row restores the values EF's curated
    ///         store holds; every Northwind discount is a two-decimal number, so the already-exact
    ///         rows (<c>0</c>, <c>0.25</c>) are rewritten with themselves.
    ///     </para>
    /// </remarks>
    protected override async Task SeedAsync(NorthwindContext context)
    {
        await base.SeedAsync(context);

        await context.Database.ExecuteSqlRawAsync(
            """UPDATE "OrderDetails" SET "Discount" = round("Discount", 2)""");
    }

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
