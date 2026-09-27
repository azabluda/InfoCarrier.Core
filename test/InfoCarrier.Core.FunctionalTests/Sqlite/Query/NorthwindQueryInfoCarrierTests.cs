// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

// Six Northwind spec-test bases with no override, on ADR-009 Tier B. Fixture generics mirror EF
// Core's own NorthwindQuery*SqliteTest classes, which are the same six with no override either.
//
// MOVED FROM TIER A ON 2026-09-27, at the owner's request: Tier A's store is EF's InMemory
// provider, which runs no statement, so #167's slow run could not compare these with plain EF. A
// seventh class of that file, the core `NorthwindDbFunctionsQueryTestBase`, did not come along:
// Tier B's `NorthwindDbFunctionsQueryInfoCarrierTest` runs the relational base, which derives from
// it, so the core tests already ran here and the Tier A class ran them a second time.
//
// Every failure is real information: only a store limit or a decision of this provider earns an
// override, with its stated reason (CLAUDE.md).

public class NorthwindAsNoTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindAsNoTrackingQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);

public class NorthwindAsTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindAsTrackingQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);

public class NorthwindChangeTrackingQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindChangeTrackingQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);

/// <remarks>
///     <b>Five tests read one row more than plain EF, and each carries a reason for it.</b> A
///     compiled query returns an <see cref="IEnumerable{T}" />, and the test takes its first element
///     with <c>First()</c>, which runs after the query and bounds nothing. Plain EF streams the rows
///     and stops reading once the test stops enumerating; this client's server reads the result to
///     its end before the rows cross the wire. Same statement, same row, one more <c>Read()</c>. Found
///     by #167's slow run when the base moved to Tier B, 2026-09-27.
/// </remarks>
public class NorthwindCompiledQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindCompiledQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc cref="ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Query_with_single_parameter()
        => base.Query_with_single_parameter();

    /// <inheritdoc cref="ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Query_with_two_parameters()
        => base.Query_with_two_parameters();

    /// <inheritdoc cref="ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Query_with_three_parameters()
        => base.Query_with_three_parameters();

    /// <inheritdoc cref="ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Query_with_contains()
        => base.Query_with_contains();

    /// <inheritdoc cref="ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Query_with_closure()
        => base.Query_with_closure();

    /// <summary>
    ///     The client evaluates what follows the query over the rows the server has already read
    ///     to the end (ADR-010); streaming results is out of scope for v10 (the owner, 2026-08-23),
    ///     so the reader runs past the row on which plain EF stops.
    /// </summary>
    internal const string ServerReadsToTheEnd =
        "The client evaluates what follows the query over the rows the server has already read to the end, and "
        + "streaming results is out of scope for v10, so the reader runs past the row on which plain EF stops.";
}

public class NorthwindQueryFiltersQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NorthwindQueryFiltersCustomizer> fixture)
    : NorthwindQueryFiltersQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NorthwindQueryFiltersCustomizer>>(fixture)
{
    /// <inheritdoc cref="NorthwindCompiledQueryInfoCarrierTest.ServerReadsToTheEnd" />
    [InfoCarrierDesign(10, Justification = NorthwindCompiledQueryInfoCarrierTest.ServerReadsToTheEnd, Deviation = DeviationKind.SqlDiffers)]
    public override void Compiled_query()
        => base.Compiled_query();
}

public class NorthwindQueryTaggingQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture);
