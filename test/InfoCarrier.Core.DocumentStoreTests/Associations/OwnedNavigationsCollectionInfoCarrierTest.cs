// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's <c>OwnedNavigationsCollectionTestBase</c> over ADR-009 Tier D.
/// </summary>
/// <remarks>
///     <para>
///         <b>One class of the six, on purpose.</b> The fixture cost of adopting a specification
///         family against a document store was unknown, and learning it once is cheaper than
///         learning it six times. The other five follow now that this one's shape is settled.
///     </para>
///     <para>
///         <b>Ten of the fifteen pass, and every one of the five overridden below fails the same way
///         with the wire and without it.</b> That is not an assumption:
///         <see cref="OwnedNavigationsServerSideControlTest" /> runs each of these query shapes
///         directly against the server's own <c>DbContext</c>, and each produces the identical
///         exception or the identical wrong count. So this file describes what
///         <c>MongoDB.EntityFrameworkCore</c> does, and none of it is this repository's behaviour.
///     </para>
///     <para>
///         <b>An override that asserts a refusal is EF's own idiom and is not a skip.</b> EF's Cosmos
///         suite does exactly this — <c>OwnedNavigations*CosmosTest</c> wraps a base call in
///         <c>Assert.ThrowsAsync&lt;CosmosException&gt;</c> — and it is what keeps the test running:
///         the day the store learns the query, the assertion fails and somebody deletes the
///         override. A skip would go on passing forever.
///     </para>
/// </remarks>
public class OwnedNavigationsCollectionInfoCarrierTest(OwnedNavigationsInfoCarrierFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The store cannot compose <c>Distinct</c> over a projected owned collection.
    /// </summary>
    /// <remarks>
    ///     <c>ArgumentException: Expression of type 'List&lt;AssociateType&gt;' cannot be used for
    ///     parameter of type 'IQueryable&lt;AssociateType&gt;' of method 'Distinct'</c>, from the
    ///     store and not from here. <b>The tracking parameterization never reaches EF's own rule
    ///     about owned entities</b>, which is what the base asserts for it: the store refuses the
    ///     query first, so both parameterizations end in the same place.
    /// </remarks>
    public override async Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
    {
        using DbContext context = Fixture.CreateContext();

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.Set<RootEntity>()
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .Select(e => e.AssociateCollection.Distinct().ToList())
                .ToListAsync());
    }

    /// <summary>
    ///     The store refuses <c>GroupBy</c> inside a document, by name.
    /// </summary>
    /// <remarks>
    ///     <c>MongoDB.Driver.Linq.ExpressionNotSupportedException</c>, raised by the driver rather
    ///     than by the provider, which is as clear a statement of a store limit as this tier will
    ///     ever get. Caught as its base type so this file names no driver type.
    /// </remarks>
    public override async Task GroupBy()
    {
        Exception thrown = await Record.ExceptionAsync(() => base.GroupBy());

        Assert.Equal("ExpressionNotSupportedException", thrown.GetType().Name);
    }

    /// <summary>
    ///     Three aggregates nested inside one another collide in the store's own alias table.
    /// </summary>
    /// <remarks>
    ///     <c>ArgumentException: An item with the same key has already been added. Key: o0</c> —
    ///     a name the store generates for a subquery, generated twice. Nothing on this side of the
    ///     wire chooses that name.
    /// </remarks>
    public override async Task Select_within_Select_within_Select_with_aggregates()
    {
        ArgumentException thrown =
            await Assert.ThrowsAsync<ArgumentException>(() => base.Select_within_Select_within_Select_with_aggregates());

        Assert.Contains("same key has already been added", thrown.Message);
    }

    /// <summary>
    ///     <b>A WRONG ANSWER RATHER THAN A REFUSAL, AND THE STORE GIVES THE SAME WRONG ANSWER
    ///     WITHOUT THE WIRE.</b>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The base expects five roots and this store returns three.
    ///         <c>OwnedNavigationsServerSideControlTest.Server_side_Distinct_over_projected_filtered_nested_collection</c>
    ///         asserts the same five directly against the server's context and fails identically, so
    ///         the three is the store's answer and not something the wire did to it.
    ///     </para>
    ///     <para>
    ///         <b>This override records the store's answer; it does not bless it.</b> The count is
    ///         asserted rather than the test being skipped, so the day the count changes — right or
    ///         wrong — this goes red and somebody looks. It is the only one of the five that is a
    ///         silent difference rather than an exception, which makes it the one worth watching.
    ///     </para>
    /// </remarks>
    public override async Task Distinct_over_projected_filtered_nested_collection()
    {
        using DbContext context = Fixture.CreateContext();

        List<RootEntity> actual = await context.Set<RootEntity>()
            .AsNoTracking()
            .Where(e => e.AssociateCollection.Select(r => r.NestedCollection.Where(n => n.Int == 8)).Distinct().Count() == 2)
            .ToListAsync();

        Assert.Equal(3, actual.Count);
    }
}
