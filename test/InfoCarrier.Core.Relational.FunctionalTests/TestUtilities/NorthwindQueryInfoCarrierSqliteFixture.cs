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
    : NorthwindQueryRelationalFixture<TModelCustomizer>
    where TModelCustomizer : ITestModelCustomizer, new()
{
    private ITestStoreFactory? _infoCarrierTestStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _infoCarrierTestStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            copyDbContextParameters: (client, server) =>
                CopyDbContextParameters((NorthwindContext)client, (NorthwindContext)server),
            // EF wraps a failed column read into `ErrorMaterializingProperty*` ONLY when detailed
            // errors are on -- the try/catch around the read is emitted by
            // `ShaperProcessingExpressionVisitor` under `if (_detailedErrorsEnabled ...)`. Here the
            // read happens on the SERVER, and a shared fixture's own `AddOptions` deliberately does
            // not reach it (A29), so the server had detailed errors off and a raw
            // `SqliteException: The data is NULL at ordinal 5` crossed where EF's own message was
            // expected. This is the server half of what the fixture already asks of the client.
            onAddOptions: b => b.EnableDetailedErrors(),
            serverContextType: typeof(NorthwindInfoCarrierSqliteServerContext),
            configureConventions: ConfigureConventions,
            relationalClientStore: true,
            arbitrarySqlExecution: true);

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

        // The table name is `NorthwindInfoCarrierSqliteServerContext`'s, which maps `OrderDetail`
        // to "Order Details" so the relational spec bases can write that name into their own raw
        // SQL. This statement is the only other place the name is written by hand, and R97 moved
        // one without the other.
        await context.Database.ExecuteSqlRawAsync(
            """UPDATE "Order Details" SET "Discount" = round("Discount", 2)""");
    }

    private static void CopyDbContextParameters(NorthwindContext client, NorthwindContext server)
        => server.TenantPrefix = client.TenantPrefix;
}
