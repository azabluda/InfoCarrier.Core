// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;

/// <summary>
///     The shared fixture for the six <c>OwnedNavigations*RelationalTestBase</c> classes below, on
///     ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>R26 re-parented this onto <c>OwnedNavigationsRelationalFixtureBase</c>.</b> C0 mapped
///         the family on the core fixture and mirrored the relational one's <c>ToTable</c> calls by
///         hand — <c>OwnedTableSplittingRelationalFixtureBase</c>'s and
///         <c>OwnedNavigationsRelationalFixtureBase</c>'s, in that order — because the test project
///         did not then reference <c>EFCore.Relational.Specification.Tests</c>. The hand copy also
///         mirrored <c>AreCollectionsOrdered</c>. All of it is now the real thing, and the copy was
///         not complete: the base also calls <c>ValueGeneratedNever()</c> on every owned key and
///         states <c>IsRequired</c> on the associate navigations, neither of which C0 carried.
///     </para>
///     <para>
///         Its own <c>StoreName</c>, per CLAUDE.md: the Tier B store is file-backed and two
///         fixtures sharing a name share a database.
///     </para>
/// </remarks>
public class OwnedNavigationsQueryInfoCarrierFixture : OwnedNavigationsRelationalFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "OwnedNavigationsQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            onAddOptions: AssociationsWarnings.ThrowOnUnorderedRowLimiting,
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     The fixture, not the base, is what carries the <c>UseTransaction</c> trap in this
    ///     family; see <see cref="NavigationsQueryInfoCarrierFixture.UseTransaction" />.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);
}

// The six OwnedNavigations facets (ADR-004), adopted bare on their relational bases: no override
// at all, so that every failure is measured before anything is written to answer it.

public class OwnedNavigationsCollectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsCollectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsMiscellaneousQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsMiscellaneousRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsPrimitiveCollectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsPrimitiveCollectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsProjectionQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsProjectionRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsSetOperationsQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsSetOperationsRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);

public class OwnedNavigationsStructuralEqualityQueryInfoCarrierTest(
    OwnedNavigationsQueryInfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : OwnedNavigationsStructuralEqualityRelationalTestBase<OwnedNavigationsQueryInfoCarrierFixture>(fixture, testOutputHelper);
