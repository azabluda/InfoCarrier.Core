// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's <c>OwnedNavigationsCollectionTestBase</c> over ADR-009 Tier D.
/// </summary>
/// <remarks>
///     <para>
///         <b>One class of the six, and the first adopted</b>, to learn what adopting this family
///         against a document store costs before doing it six times. The other five are in
///         <c>OwnedNavigationsInfoCarrierTests.cs</c>.
///     </para>
///     <para>
///         <b>EVERY OVERRIDE HERE SAYS WHAT THE STORE DOES AND WHERE THAT IS SHOWN.</b> The attribute
///         is the label; its arguments name the test of <see cref="DirectCollectionTest" /> that shows
///         the same behaviour with InfoCarrier removed. <c>OverrideAudit</c> fails the build if the
///         two disagree. <c>docs/plans/v10/test-overhaul.md</c> is the reading.
///     </para>
///     <para>
///         <b>This class held four overrides with no reference until 2026-09-14</b>, argued from EF's
///         Cosmos idiom, and one of them asserted three roots where five are correct. Cosmos is not
///         MongoDB, and that override is now <see cref="Distinct_over_projected_filtered_nested_collection" />:
///         a <c>DEFECT</c> with its section in <c>docs/upstream-defects.md</c>, rather than an
///         unexplained number.
///     </para>
/// </remarks>
public class OwnedNavigationsCollectionInfoCarrierTest(OwnedNavigationsInfoCarrierFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsInfoCarrierFixture>(fixture)
{
    /// <summary>Grouping inside a document is refused by the driver, by name.</summary>
    [StoreLimit(typeof(DirectCollectionTest), nameof(DirectCollectionTest.GroupBy))]
    public override Task GroupBy()
        => StoreBehaviour.Refuses(base.GroupBy, "ExpressionNotSupportedException");

    /// <summary><c>Distinct</c> over a projected owned collection does not translate.</summary>
    [StoreLimit(typeof(DirectCollectionTest), nameof(DirectCollectionTest.Distinct_projected))]
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? StoreBehaviour.BaseExpectedAnotherException(
                () => base.Distinct_projected(queryTrackingBehavior),
                nameof(ArgumentException),
                "cannot be used for parameter of type")
            : StoreBehaviour.Refuses(() => base.Distinct_projected(queryTrackingBehavior), nameof(ArgumentException));

    /// <summary>A silent wrong answer from the store: three roots where five are correct.</summary>
    [StoreDefect(
        "1.7",
        typeof(DirectCollectionTest),
        nameof(DirectCollectionTest.Distinct_over_projected_filtered_nested_collection))]
    public override Task Distinct_over_projected_filtered_nested_collection()
        => StoreBehaviour.BaseExpectedAnotherValue(base.Distinct_over_projected_filtered_nested_collection, 5, 3);

    /// <summary>Three nested aggregates collide in the store's own subquery alias table.</summary>
    [StoreDefect(
        "1.8",
        typeof(DirectCollectionTest),
        nameof(DirectCollectionTest.Select_within_Select_within_Select_with_aggregates))]
    public override Task Select_within_Select_within_Select_with_aggregates()
        => StoreBehaviour.Refuses(base.Select_within_Select_within_Select_with_aggregates, nameof(ArgumentException));
}
