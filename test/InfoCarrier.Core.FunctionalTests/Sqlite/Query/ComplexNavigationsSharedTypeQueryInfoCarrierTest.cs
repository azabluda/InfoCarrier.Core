// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ComplexNavigationsSharedTypeQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The complex-navigations corpus again, over a model whose levels are <em>shared-type</em>
///         entity types — the same navigation depth, but every entity type is keyed by name and
///         several share one CLR type. That is the case where reading a value by its CLR member is
///         not enough and the model has to be consulted, which is most of what this provider's
///         mapper does.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The relational fixture base maps all
///         four levels onto one table, so on a real database this is table splitting as well as
///         shared types — a shape InMemory could not express at all. Neither base declares
///         <c>UseTransaction</c> nor calls <c>ExecuteWithStrategyInTransactionAsync</c>.
///     </para>
/// </remarks>
public class ComplexNavigationsSharedTypeQueryInfoCarrierTest(ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsSharedTypeQueryRelationalTestBase<ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture);

/// <summary>
///     <c>ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase</c> on Tier B — the same
///     model, queried through its collections.
/// </summary>
/// <remarks>
///     The relational base adds one override of its own, for the join-key ambiguity a relational
///     provider reports. The Tier A class it replaces carried seven overrides for InMemory's
///     non-composed <c>GroupBy</c> limitation, and all seven are gone.
/// </remarks>
public class ComplexNavigationsCollectionsSharedTypeQueryInfoCarrierTest(
    ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsSharedTypeQueryRelationalTestBase<
        ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The shared-type complex-navigations fixture, wired to a SQLite backend behind the wire.
///     Shared by both classes above, exactly as EF shares its own.
/// </summary>
public class ComplexNavigationsSharedTypeQueryInfoCarrierFixture : ComplexNavigationsSharedTypeQueryRelationalFixtureBase
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
