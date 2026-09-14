// Licensed under the MIT license. See license.txt file in the project root for license information.

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
///         <b>ONE OVERRIDE, AND THIS CLASS HELD FOUR UNTIL 2026-09-14.</b> Ten of the fifteen
///         tests pass, <c>GroupBy</c> is overridden against a citation, and the remaining four are
///         RED, classified in <c>test/known-failures.txt</c> and gated by the ratchet like every
///         other tier's. The three that lost their override asserted what the store does — two
///         refusals and one WRONG COUNT — and the argument for them was EF's own Cosmos idiom,
///         which really does wrap a base call in <c>Assert.ThrowsAsync</c>.
///     </para>
///     <para>
///         <b>That argument was wrong here, and the reason is worth keeping: COSMOS IS NOT
///         MONGODB.</b> They are both document stores and that is where it ends — different
///         products, different providers, different teams. An override needs a citation to the
///         store this tier actually runs, and the one below has one. The three that lost theirs do
///         not, and one of them was not a refusal at all: it asserted three roots where five are
///         correct, so the suite reported green over a wrong answer.
///     </para>
///     <para>
///         <b>The attribution did not depend on the overrides and has not moved.</b>
///         <see cref="OwnedNavigationsServerSideControlTest" /> runs each of the five shapes
///         directly against the server's own <c>DbContext</c> and gets the identical exception or
///         the identical wrong count, so the five reds are <c>MongoDB.EntityFrameworkCore</c>'s
///         behaviour and not this repository's. <b>A control answers whose defect it is; it never
///         decided whether a red is allowed.</b> Conflating those two is what produced the
///         overrides.
///     </para>
///     <para>
///         <b>Nothing is lost by going red, which was the objection to it.</b> The day the store
///         learns one of these queries, the test passes, the ratchet's name diff reports it, and
///         somebody removes a line from the baseline. That is the same signal the override gave by
///         failing, arriving through the mechanism every other tier already uses.
///     </para>
/// </remarks>
public class OwnedNavigationsCollectionInfoCarrierTest(OwnedNavigationsInfoCarrierFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     <c>GroupBy</c> inside a document: declared unsupported by the store, twice over.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>THE ONLY OVERRIDE IN THIS CLASS, AND THE CITATION IS WHY.</b>
    ///         <c>MongoDB.EntityFrameworkCore</c> declares <c>GroupBy</c> unsupported in its own
    ///         <c>FunctionalTests/Query/UnsupportedQueryTests.cs</c>, and its
    ///         <c>NorthwindGroupByQueryMongoTest</c> overrides roughly two hundred tests against
    ///         tracked issue <b>EF-149</b>, asserting this same
    ///         <c>ExpressionNotSupportedException</c>. A store that refuses <c>GroupBy</c> leaves
    ///         this provider nothing to answer with, so a red here would restate the obvious.
    ///     </para>
    ///     <para>
    ///         The other four reds in this class have NO such citation and stay red: their
    ///         <c>Distinct</c> behaviour over an owned collection is untested upstream, and one of
    ///         the four is not a refusal at all but a silent wrong count.
    ///     </para>
    ///     <para>
    ///         Caught by type NAME so that this file names no driver type, which is the idiom
    ///         <see cref="OwnedNavigationsServerSideControlTest" /> already uses.
    ///     </para>
    /// </remarks>
    public override async Task GroupBy()
    {
        Exception thrown = await Record.ExceptionAsync(() => base.GroupBy());

        Assert.Equal("ExpressionNotSupportedException", thrown.GetType().Name);
    }
}
