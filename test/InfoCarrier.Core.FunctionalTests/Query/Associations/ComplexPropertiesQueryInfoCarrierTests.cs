// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query.Associations;

/// <summary>
///     The shared fixture for the seven <c>ComplexProperties*TestBase</c> classes below, on ADR-009
///     <b>Tier B</b> (C0).
/// </summary>
/// <remarks>
///     <para>
///         <b>This is A77's verdict being cashed.</b> A77 tried the complex-property query family
///         on Tier A, found that EF's InMemory provider does not translate a complex property
///         access at all, and concluded "not adoptable". Phase B's rule corrects that to "Tier B" —
///         the same correction A79 made for <c>FunkyDataQuery</c>, and the reason EF ships no
///         InMemory test here is the reason it belongs on the store that translates.
///     </para>
///     <para>
///         The core fixture stands alone: it maps the whole complex graph, references and
///         collections, plus the <c>ValueRootEntity</c> tree that exists because value types are
///         only supported as complex types. The relational assembly's two variants are
///         <c>ComplexJson</c> and <c>ComplexTableSplitting</c> — mapping strategies, not this
///         family — so unlike C2 there is nothing to mirror.
///     </para>
/// </remarks>
public class ComplexPropertiesQueryInfoCarrierFixture : ComplexPropertiesFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "ComplexPropertiesQueryInfoCarrierTest";

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     <c>AssociationsQueryFixtureBase.UseTransaction</c> throws <c>NotSupportedException</c>
    ///     and every relational fixture overrides it, because the base runs each bulk-update test
    ///     inside a transaction and makes a second context observe the same uncommitted state.
    ///     <b>All 31 <c>ComplexPropertiesBulkUpdate</c> failures were this and not
    ///     <c>ExecuteUpdate</c> at all</b> — the stack named the fixture one frame in. The provider
    ///     has its own enlistment (Phase T / M4), which <c>StoreGenerated</c>,
    ///     <c>ConferencePlanner</c> and <c>OptimisticConcurrency</c> already use.
    /// </remarks>
    public override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         The <c>ToJson()</c> calls are <c>ComplexJsonRelationalFixtureBase</c>'s, ~20 lines,
    ///         mirrored by hand. <b>Without them nothing runs at all</b> — 134 of 136 tests failed
    ///         identically on <i>"The complex collection property 'RootEntity.AssociateCollection'
    ///         must be mapped to a JSON column"</i>. That is not a preference: a relational store
    ///         has no other way to hold a complex collection, so on Tier B this mapping is what the
    ///         family <em>is</em>.
    ///     </para>
    ///     <para>
    ///         Which makes this the one place C0's "we adopt the core family, not the
    ///         mapping-strategy variants" line bends, and it is worth being explicit about why it
    ///         is not broken: what is mirrored is the <em>mapping</em>, not the
    ///         <c>ComplexJson*TestBase</c> classes. Those assert SQL and stay unadopted; the seven
    ///         classes below are still the core ones.
    ///     </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        modelBuilder.Entity<RootEntity>(b =>
        {
            b.ComplexProperty(e => e.RequiredAssociate, rrb => rrb.ToJson());
            b.ComplexProperty(e => e.OptionalAssociate, orb => orb.ToJson());
            b.ComplexCollection(e => e.AssociateCollection, rcb => rcb.ToJson());
        });

        modelBuilder.Entity<ValueRootEntity>(b =>
        {
            b.ComplexProperty(e => e.RequiredAssociate, rrb => rrb.ToJson());

            b.ComplexProperty(e => e.OptionalAssociate, orb =>
            {
                orb.ToJson();

                // EF's own note: without this, the model reports an ambiguous property.
                orb.ComplexProperty(r => r.OptionalNested).IsRequired(false);
            });

            b.ComplexCollection(e => e.AssociateCollection, rcb => rcb.ToJson());
        });
    }
}

// The seven ComplexProperties facets (ADR-004), starting with no overrides. Every failure is real
// information, triaged in docs/implementation-plan.md under C3.

public class ComplexPropertiesBulkUpdateQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesBulkUpdateTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesCollectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesCollectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesMiscellaneousQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesMiscellaneousTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesPrimitiveCollectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesPrimitiveCollectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesProjectionQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesProjectionTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesSetOperationsQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesSetOperationsTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);

public class ComplexPropertiesStructuralEqualityQueryInfoCarrierTest(ComplexPropertiesQueryInfoCarrierFixture fixture)
    : ComplexPropertiesStructuralEqualityTestBase<ComplexPropertiesQueryInfoCarrierFixture>(fixture);
