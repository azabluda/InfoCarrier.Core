// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The four query shapes that fail in <see cref="OwnedNavigationsCollectionInfoCarrierTest" />,
///     run directly against the server with InfoCarrier out of the picture.
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
///         are the first kind, which is what <c>test/known-failures.txt</c> records against those
///         five red tests.
///     </para>
///     <para>
///         <b>A CONTROL ANSWERS WHOSE DEFECT IT IS. IT NEVER DECIDED WHETHER A RED IS ALLOWED, AND
///         THAT CONFLATION COST FOUR OVERRIDES (2026-09-14).</b> The class above used to assert
///         these same outcomes on the specification tests themselves, which turned the suite green
///         while the store returned three roots where five are correct. The control is unchanged by
///         that correction, because it was never the part that was wrong: it is a hand-written test
///         about the store, not a specification test about the wire.
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

    /// <summary>
    ///     Projecting a nested associate THROUGH an optional associate that is null on some rows.
    /// </summary>
    /// <remarks>
    ///     <b>The specification test fails with <c>NullReferenceException</c>, which is the shape a
    ///     WIRE defect takes</b> - a materializer reaching through a null owned reference. That is
    ///     why this is a control rather than an assumption: it runs the identical projection with
    ///     InfoCarrier out of the picture.
    /// </remarks>
    [Fact]
    public async Task Server_side_Select_required_nested_on_optional_associate()
    {
        using DbContext db = Server();

        Exception? thrown = await Record.ExceptionAsync(
            () => db.Set<RootEntity>()
                .AsNoTracking()
                .Select(x => x.OptionalAssociate!.RequiredNestedAssociate)
                .ToListAsync());

        Assert.Equal("NullReferenceException", thrown?.GetType().Name);
    }

    /// <inheritdoc cref="Server_side_Select_required_nested_on_optional_associate" />
    [Fact]
    public async Task Server_side_Select_optional_nested_on_optional_associate()
    {
        using DbContext db = Server();

        Exception? thrown = await Record.ExceptionAsync(
            () => db.Set<RootEntity>()
                .AsNoTracking()
                .Select(x => x.OptionalAssociate!.OptionalNestedAssociate)
                .ToListAsync());

        Assert.Equal("NullReferenceException", thrown?.GetType().Name);
    }

    /// <summary>
    ///     Structural equality against an inline nested owned associate, which EF's own base says
    ///     must throw (EF #36400) and this store answers instead.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A STORE MORE CAPABLE THAN THE SPECIFICATION IS STILL A DIFFERENCE, AND THE ANSWER
    ///         HAS TO BE CHECKED.</b> <c>OwnedNavigationsStructuralEqualityTestBase</c> wraps this
    ///         in <c>Assert.ThrowsAsync&lt;InvalidOperationException&gt;</c>, so when nothing
    ///         throws the base never compares the rows and nobody learns whether the answer was
    ///         right. That is the identical trap
    ///         <c>Distinct_over_projected_filtered_nested_collection</c> laid, where the store
    ///         quietly returned three rows in place of five.
    ///     </para>
    ///     <para>
    ///         So this control asserts the count the base's own seed data implies. If the store
    ///         answers differently, or starts refusing as EF expects, it goes red and is read.
    ///     </para>
    /// </remarks>
    [Fact]
    public async Task Server_side_Nested_associate_with_inline()
    {
        using DbContext db = Server();

        List<RootEntity> result = await db.Set<RootEntity>()
            .AsNoTracking()
            .Where(e => e.RequiredAssociate.RequiredNestedAssociate
                == new NestedAssociateType
                {
                    Id = 1000,
                    Name = "Root1_RequiredAssociate_RequiredNestedAssociate",
                    Int = 8,
                    String = "foo",
                    // Not a collection expression: an expression tree may not contain one (CS9175).
                    Ints = new List<int> { 1, 2, 3 },
                })
            .ToListAsync();

        Assert.Single(result);
    }
}
