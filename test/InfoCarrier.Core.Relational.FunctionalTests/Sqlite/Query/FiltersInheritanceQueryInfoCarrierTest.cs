// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     The filtered variants of the three inheritance mappings, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>A global query filter over an inheritance hierarchy is the interesting combination</b>,
///         and it is the reason these three are adopted together with the unfiltered ones rather
///         than later. A filter on <c>Animal</c> has to reach every derived type, and under TPT and
///         TPC those types live in different store objects. The filter marker crosses the wire and
///         the server applies it, so what is measured here is that this client hands over a model
///         whose hierarchy the server resolves the same way.
///     </para>
///     <para>
///         <b>Each fixture derives from the unfiltered one in this repository, not from EF's.</b>
///         EF's own SQLite fixtures do the same thing one level up —
///         <c>TPTFiltersInheritanceQuerySqliteFixture : TPTInheritanceQuerySqliteFixture</c> — so
///         the store wiring is stated once and <c>EnableFilters</c> is the whole difference.
///     </para>
///     <para>
///         <b>There is no TPH member here, and its absence is the rule rather than an omission.</b>
///         EF's <c>TPHFiltersInheritanceQuerySqliteTest</c> derives from the <em>core</em>
///         <c>FiltersInheritanceQueryTestBase</c>, which
///         <c>InMemory.Query.FiltersInheritanceQueryInfoCarrierTest</c> already adopts on Tier A.
///         ADR-009 gives a base exactly one tier, so a second class over the same base would be
///         duplication and not coverage.
///     </para>
///     <para>
///         <b>No <c>UseTransaction</c> override, and that is checked rather than assumed.</b> These
///         bases descend from <c>FiltersInheritanceQueryTestBase</c> -&gt;
///         <c>FilteredQueryTestBase</c>, not from <c>InheritanceQueryTestBase</c>, so nothing here
///         calls <c>ExecuteWithStrategyInTransactionAsync</c>. EF's own SQLite classes are
///         one-liners with no override for the same reason.
///     </para>
/// </remarks>
public class TPTFiltersInheritanceQueryInfoCarrierTest(TPTFiltersInheritanceQueryInfoCarrierFixture fixture)
    : TPTFiltersInheritanceQueryTestBase<TPTFiltersInheritanceQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="TPTFiltersInheritanceQueryInfoCarrierTest" />
public class TPCFiltersInheritanceQueryInfoCarrierTest(TPCFiltersInheritanceQueryInfoCarrierFixture fixture)
    : TPCFiltersInheritanceQueryTestBase<TPCFiltersInheritanceQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The TPT inheritance fixture with global query filters on.
/// </summary>
public class TPTFiltersInheritanceQueryInfoCarrierFixture : TPTInheritanceQueryInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;
}

/// <summary>
///     The TPC inheritance fixture with global query filters on.
/// </summary>
/// <remarks>
///     <c>UseGeneratedKeys</c> is restated rather than inherited, exactly as EF's own
///     <c>TPCFiltersInheritanceQuerySqliteFixture</c> restates it.
/// </remarks>
public class TPCFiltersInheritanceQueryInfoCarrierFixture : TPCInheritanceQueryInfoCarrierFixture
{
    /// <inheritdoc />
    public override bool EnableFilters
        => true;

    /// <inheritdoc />
    public override bool UseGeneratedKeys
        => false;
}
