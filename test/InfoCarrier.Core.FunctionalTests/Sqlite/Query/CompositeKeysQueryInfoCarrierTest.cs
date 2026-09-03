// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>CompositeKeysQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         A whole query base over a model keyed on two properties. <c>CompositeKeyEndToEnd</c>
///         covers the tracking side; this is the query side. Every identity path here builds a key
///         array, and a query that projects, joins or includes across one is where an off-by-one
///         in that array shows.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The relational base adds no methods
///         of its own, so the move is not about new tests — it is about the ones that already
///         existed running against a real database rather than InMemory. This is the cheapest
///         possible instance of that move: EF's own class is a one-liner, the base declares no
///         <c>UseTransaction</c> and calls the transaction helper zero times, so nothing here needs
///         an override. Both were checked rather than assumed.
///     </para>
/// </remarks>
public class CompositeKeysQueryInfoCarrierTest(CompositeKeysQueryInfoCarrierFixture fixture)
    : CompositeKeysQueryRelationalTestBase<CompositeKeysQueryInfoCarrierFixture>(fixture);

/// <summary>
///     <c>CompositeKeysSplitQueryRelationalTestBase</c> on Tier B — the composite-key corpus with
///     <c>AsSplitQuery</c> applied at every query root.
/// </summary>
/// <remarks>
///     No overrides, and none is needed: the hint is removed before the boundary analysis, so this
///     class asks the server exactly what the class above asks it, and every test answers the
///     same. EF's own <c>CompositeKeysSplitQuerySqliteTest</c> is a one-liner too.
/// </remarks>
public class CompositeKeysSplitQueryInfoCarrierTest(CompositeKeysQueryInfoCarrierFixture fixture)
    : CompositeKeysSplitQueryRelationalTestBase<CompositeKeysQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The composite-keys query fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class CompositeKeysQueryInfoCarrierFixture : CompositeKeysQueryRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
