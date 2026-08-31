// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexTableSplitting;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the five <c>ComplexTableSplitting*RelationalTestBase</c> classes
///     below, on ADR-009 <b>Tier B</b> (R28).
/// </summary>
/// <remarks>
///     <para>
///         <b>A new family.</b> It is the complex-type counterpart of
///         <see cref="OwnedTableSplittingQueryInfoCarrierFixture" />: the complex property's
///         members become columns in the owner's own table. <b>Collections are not expressible that
///         way at all</b> — only JSON can hold one — so
///         <c>ComplexTableSplittingRelationalFixtureBase</c> both <c>Ignore</c>s every collection
///         in the model and nulls it out of the seed data, and a large share of these classes are
///         assertions that a collection query fails.
///     </para>
///     <para>
///         <b>This is the same model that C0 called the one bend in "we adopt the core family, not
///         the mapping-strategy variants".</b>
///         <see cref="ComplexPropertiesQueryInfoCarrierFixture" /> mirrors
///         <c>ComplexJsonRelationalFixtureBase</c>'s <c>ToJson()</c> by hand because a relational
///         store has no other way to hold a complex collection. This family is the other answer to
///         the same question: do not hold collections at all. Both are now adopted as EF states
///         them.
///     </para>
///     <para>
///         Nothing is mirrored by hand. Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store
///         is file-backed and two fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class ComplexTableSplittingQueryInfoCarrierFixture : ComplexTableSplittingRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "ComplexTableSplittingQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     Required, and doubly so here: the fixture base declares it with
    ///     <c>transaction.GetDbTransaction()</c> (ADR-013), and the <c>BulkUpdate</c> class really
    ///     does run each test inside a transaction that a second context has to observe. That is
    ///     the 31-failure lesson recorded on
    ///     <see cref="ComplexPropertiesQueryInfoCarrierFixture.UseTransaction" />.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The five ComplexTableSplitting facets (ADR-004), adopted bare: no override at all, so that
// every failure is measured and classified before anything is written to answer it. Five and not
// seven: EF ships no Collection or SetOperations class for this family, which follows from the
// model -- there are no collections in it to run them against.

public class ComplexTableSplittingBulkUpdateQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingBulkUpdateRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingMiscellaneousQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingMiscellaneousRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingPrimitiveCollectionQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingPrimitiveCollectionRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingProjectionQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingProjectionRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class ComplexTableSplittingStructuralEqualityQueryInfoCarrierTest(
    ComplexTableSplittingQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTableSplittingStructuralEqualityRelationalTestBase<ComplexTableSplittingQueryInfoCarrierFixture>(fixture, testOutputHelper);
