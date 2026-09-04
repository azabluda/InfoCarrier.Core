// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>GearsOfWarFromSqlQueryTestBase</c> on ADR-009 <b>Tier B</b> (#60).
/// </summary>
/// <remarks>
///     <para>
///         <b>One test, and it needs a fixture nothing else here has.</b> The base is constrained
///         to <c>GearsOfWarQueryRelationalFixture</c>, and this suite's Gears of War fixtures are
///         the TPT and TPC ones, which derive from <c>GearsOfWarQueryFixtureBase</c> instead - a
///         sibling, not a parent. So the fixture below is new, and it brings a store of its own.
///     </para>
///     <para>
///         <b>Adopted anyway, and the reason is policy rather than the count.</b> EF ships a SQLite
///         class for this base (<c>GearsOfWarFromSqlQuerySqliteTest</c>, itself a one-liner), so
///         "EF ships no test for it on any store we have" - the only justification CLAUDE.md
///         accepts for leaving a base unadopted - does not apply. What the base checks is worth
///         having on its own: <c>SELECT</c> with the columns in an order the model does not
///         declare, materialized by name rather than by position.
///     </para>
/// </remarks>
public class GearsOfWarFromSqlQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierSqliteFixture fixture)
    : GearsOfWarFromSqlQueryTestBase<GearsOfWarQueryInfoCarrierSqliteFixture>(fixture);

/// <summary>
///     The single-table Gears of War fixture, wired to a SQLite backend behind the wire.
/// </summary>
/// <remarks>
///     Raw SQL is granted on both halves, which is the whole reason this fixture exists; the
///     relational client store is what lets the base normalize the delimiters of the SQL it writes.
///     See <c>docs/security-review.md</c> section 5a for what the first of those grants.
/// </remarks>
public class GearsOfWarQueryInfoCarrierSqliteFixture : GearsOfWarQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions,
            relationalClientStore: true,
            arbitrarySqlExecution: true);
}
