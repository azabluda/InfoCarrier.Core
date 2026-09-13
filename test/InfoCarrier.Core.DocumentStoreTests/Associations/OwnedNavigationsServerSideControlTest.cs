// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The four query shapes <see cref="OwnedNavigationsCollectionInfoCarrierTest" /> has to
///     override, run directly against the server with InfoCarrier out of the picture.
/// </summary>
/// <remarks>
///     <para>
///         <b>THIS CLASS IS A CONTROL AND NOTHING ELSE</b>, and it is the same fork
///         <c>ServerSideControlTest</c> is for the write path. A red specification test on this tier
///         is not evidence of anything until somebody has asked whether the store answers the query
///         at all, and the official MongoDB provider's own suite makes that question live: its
///         <c>MongoComplianceTest</c> ignores all six <c>OwnedNavigations</c> bases as "not yet
///         overridden", so nobody had run them against this store before.
///     </para>
///     <para>
///         <b>Read it as a fork.</b> A query that behaves the same here as over the wire is the
///         store's business; one that passes here and fails over the wire is ours. All four below
///         are the first kind, which is why the class above overrides rather than reports.
///     </para>
///     <para>
///         <b>THESE ASSERTIONS RECORD WHAT THE STORE DOES; THEY DO NOT SAY IT IS RIGHT.</b> Three
///         assert an exception and the fourth asserts a count the base says should be five. Writing
///         the observed number down is what makes the control useful: when the store changes, this
///         goes red and somebody reads it, where a skip would stay quiet forever.
///     </para>
///     <para>
///         <b>The fourth one nearly fooled this session.</b> It first asserted only that the query
///         did not throw, which it does not, and the wire's wrong count then looked like ours. A
///         control has to assert the same thing the specification base asserts, or it answers a
///         question nobody asked.
///     </para>
/// </remarks>
public class OwnedNavigationsServerSideControlTest(OwnedNavigationsControlFixture fixture)
    : IClassFixture<OwnedNavigationsControlFixture>
{
    private DbContext Server()
        => ((IInfoCarrierClientTestStore)fixture.TestStore).Backend.CreateDbContext();

    /// <summary>The store refuses <c>Distinct</c> over a projected owned collection.</summary>
    [Fact]
    public async Task Server_side_Distinct_projected()
    {
        using DbContext db = Server();

        await Assert.ThrowsAsync<ArgumentException>(
            () => db.Set<RootEntity>()
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .Select(e => e.AssociateCollection.Distinct().ToList())
                .ToListAsync());
    }

    /// <summary>
    ///     The store answers this one, and answers it with three where the base expects five.
    /// </summary>
    /// <remarks>
    ///     The only silent difference of the four. Asserted as three so that a change of answer is
    ///     visible; see the class remarks for why that is not the same as calling three correct.
    /// </remarks>
    [Fact]
    public async Task Server_side_Distinct_over_projected_filtered_nested_collection()
    {
        using DbContext db = Server();

        List<RootEntity> result = await db.Set<RootEntity>()
            .AsNoTracking()
            .Where(e => e.AssociateCollection.Select(r => r.NestedCollection.Where(n => n.Int == 8)).Distinct().Count() == 2)
            .ToListAsync();

        Assert.Equal(3, result.Count);
    }

    /// <summary>The driver refuses <c>GroupBy</c> inside a document, by name.</summary>
    [Fact]
    public async Task Server_side_GroupBy()
    {
        using DbContext db = Server();

        Exception thrown = await Record.ExceptionAsync(
            () => db.Set<RootEntity>()
                .AsNoTracking()
                .Where(e => e.AssociateCollection.GroupBy(r => r.String)
                    .Select(g => g.Sum(int (AssociateType r) => r.Int)).Any(g => g == 16))
                .ToListAsync());

        Assert.Equal("ExpressionNotSupportedException", thrown.GetType().Name);
    }

    /// <summary>Three nested aggregates collide in the store's own alias table.</summary>
    [Fact]
    public async Task Server_side_Select_within_Select_within_Select_with_aggregates()
    {
        using DbContext db = Server();

        ArgumentException thrown = await Assert.ThrowsAsync<ArgumentException>(
            () => db.Set<RootEntity>()
                .AsNoTracking()
                .Select(e => e.AssociateCollection.Select(r => r.NestedCollection.Select(n => n.Int).Max()).Sum())
                .ToListAsync());

        Assert.Contains("same key has already been added", thrown.Message);
    }
}
