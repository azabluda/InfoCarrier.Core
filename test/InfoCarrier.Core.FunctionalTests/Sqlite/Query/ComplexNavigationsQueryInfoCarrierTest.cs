// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ComplexNavigationsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The deepest navigation corpus EF has: optional and required one-to-one chains four
///         levels down, each level a separate entity type. Where <c>InheritanceRelationships</c>
///         tests which type a navigation names, this tests how far a query can walk before the
///         split has to decide what travels.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The relational base adds no test
///         methods of its own — it overrides one client-evaluation test and swaps in
///         <c>RelationalQueryAsserter</c> — so the move is not about new tests. It is about the
///         ones that already existed running against a real database rather than InMemory.
///         Neither the core base nor the relational one declares <c>UseTransaction</c> or calls
///         <c>ExecuteWithStrategyInTransactionAsync</c>, both checked rather than assumed, so
///         nothing here needs a transaction override.
///     </para>
/// </remarks>
public class ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsQueryRelationalTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture);

/// <summary>
///     <c>ComplexNavigationsCollectionsQueryRelationalTestBase</c> on Tier B — the same model,
///     queried through its collections.
/// </summary>
/// <remarks>
///     EF's relational base is a one-liner. The Tier A class it replaces carried seven overrides
///     for InMemory's non-composed <c>GroupBy</c> limitation, and a real database has no such
///     limitation, so all seven are gone.
/// </remarks>
public class ComplexNavigationsCollectionsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsQueryRelationalTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The complex-navigations fixture, wired to a SQLite backend behind the wire. Shared by both
///     classes above, exactly as EF shares its own.
/// </summary>
/// <remarks>
///     <c>ComplexNavigationsQueryRelationalFixtureBase</c> implements
///     <c>ITestSqlLoggerFactory</c>, which is what the compliance test's second assertion asks of
///     a relational query fixture.
/// </remarks>
public class ComplexNavigationsQueryInfoCarrierFixture : ComplexNavigationsQueryRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
