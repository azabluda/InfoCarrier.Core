// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's whole <c>OwnedNavigations</c> family against MongoDB with InfoCarrier removed. The
///     control for Tier D, and the citation behind every override in it.
/// </summary>
/// <remarks>
///     <para>
///         <b>A RED ON TIER D IS NOT EVIDENCE UNTIL SOMEBODY ASKS WHETHER THE STORE CAN ANSWER THE
///         QUERY AT ALL.</b> Six classes here run the identical bases on plain EF Core over the
///         same embedded MongoDB. Red here and red on Tier D is the store's; red on Tier D alone is
///         ours. It answers for every test rather than the ones somebody thought to ask about.
///     </para>
///     <para>
///         <b>THESE CLASSES DOCUMENT THE STORE; THEY DO NOT FAIL.</b> Each override below asserts
///         what MongoDB actually does, so the class is green and the day the store does something
///         else it goes red. A control whose result is a bare failure says only "this did not
///         work"; one that asserts the exception says what happened. <c>docs/plans/v10/test-overhaul.md</c>
///         carries the reasoning, and the label on each override is from its table.
///     </para>
///     <para>
///         <b>THREE LABELS, AND THE DIFFERENCE MATTERS.</b> <c>LIMIT</c> is a store design limit and
///         needs no issue. <c>DEFECT</c> is a crash or a wrong answer and belongs in
///         <c>docs/upstream-defects.md</c>. <c>ISSUE</c> names their own tracker entry. A crash is
///         never labelled <c>LIMIT</c>: a store that means "no" says so.
///     </para>
///     <para>
///         <b>NOT YET COMPLETE.</b> 25 of the 40 failures are overridden here. The remaining 15 fail
///         inside the specification base's own assertion rather than raising the store's exception,
///         so each needs its query written out by hand to assert a real outcome. Those stay red
///         until then, and <c>test/known-failures.txt</c> counts them.
///     </para>
///     <para>
///         <b>AND IT IS GATED, BECAUSE IT IS A CONFLICT OF INTEREST.</b> The worse this control is
///         wired the more it fails, and the more of Tier D's reds are attributed to the store rather
///         than to us. <c>eng/tier-d-control.py</c> checks that. The first version of this control
///         had exactly that bug: it passed <c>null</c> for the model customization, so its own
///         server context would have used a different model from the one Tier D measures.
///     </para>
/// </remarks>
public class OwnedNavigationsDirectStoreFixture : OwnedNavigationsFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= new MongoDirectTestStoreFactory(
            (modelBuilder, context) => OnModelCreating(modelBuilder, context));

    /// <inheritdoc />
    /// <remarks>
    ///     A store of its own, because one server per test class is this tier's rule and this
    ///     fixture is shared by six classes.
    /// </remarks>
    protected override string StoreName => "OwnedNavigationsDirect";
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>ISSUE EF-149. The store declares GroupBy unsupported and refuses it by name.</summary>
    public override Task GroupBy()
        => StoreBehaviour.Refuses(base.GroupBy, "ExpressionNotSupportedException");

    /// <summary>
    ///     LIMIT. The store's translator will not compose <c>Distinct</c> over a projected owned
    ///     collection. Only the untracked arm reaches the store; see the class remarks.
    /// </summary>
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Distinct_projected(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Distinct_projected(queryTrackingBehavior), nameof(ArgumentException));

    /// <summary>
    ///     DEFECT. Three nested aggregates collide in the store's own subquery alias table:
    ///     <c>An item with the same key has already been added. Key: o0</c>. See
    ///     <c>docs/upstream-defects.md</c> §1.8.
    /// </summary>
    public override Task Select_within_Select_within_Select_with_aggregates()
        => StoreBehaviour.Refuses(
            base.Select_within_Select_within_Select_with_aggregates, nameof(ArgumentException));
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectMiscellaneousTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsMiscellaneousTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectPrimitiveCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsPrimitiveCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectProjectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsProjectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     LIMIT. <c>SelectMany</c> over an owned collection is declared unsupported by the store's
    ///     own <c>FunctionalTests/Query/UnsupportedQueryTests.cs</c>, on an embedded array.
    /// </summary>
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(queryTrackingBehavior, () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <summary>LIMIT. A subquery inside a projection is not translated.</summary>
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(
            queryTrackingBehavior, () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(
            queryTrackingBehavior, () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => Untracked(
            queryTrackingBehavior, () => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));

    /// <summary>LIMIT. The driver refuses an unmapped property by name.</summary>
    public override Task Select_unmapped_associate_scalar_property(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_unmapped_associate_scalar_property(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_unmapped_associate_scalar_property(queryTrackingBehavior),
                "ExpressionNotSupportedException");

    /// <summary>
    ///     ISSUE EF-250, and the behaviour is narrower than that issue's title.
    /// </summary>
    /// <remarks>
    ///     <i>"Allow client evaluation in the final projection"</i> is Closed and Fixed in provider
    ///     10.0.3, which this tier measures. The fix reaches a BCL instance method and not a
    ///     user-defined static one. <see cref="ClientEvaluatedProjectionTest" /> measures both and
    ///     rules out the owned-reference hop. Both tracking arms refuse here.
    /// </remarks>
    public override Task Select_untranslatable_method_on_associate_scalar_property(
        QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Select_untranslatable_method_on_associate_scalar_property(queryTrackingBehavior),
            "ExpressionNotSupportedException");

    /// <summary>
    ///     DEFECT on one arm, LIMIT on the other, and they are different failures.
    /// </summary>
    /// <remarks>
    ///     Under <c>TrackAll</c> EF states its own rule about tracking an owned entity without its
    ///     owner, which is a refusal. Under <c>NoTracking</c> the store raises a
    ///     <c>NullReferenceException</c>: a crash, recorded in <c>docs/upstream-defects.md</c> §1.6.
    /// </remarks>
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior),
            queryTrackingBehavior is QueryTrackingBehavior.TrackAll
                ? nameof(InvalidOperationException)
                : nameof(NullReferenceException));

    /// <summary>
    ///     DEFECT. Projecting a nested associate through a null optional owned reference crashes.
    ///     See <c>docs/upstream-defects.md</c> §1.6.
    /// </summary>
    public override Task Select_required_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_required_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_required_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <inheritdoc cref="Select_required_nested_on_optional_associate" />
    public override Task Select_optional_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_optional_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_optional_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <summary>
    ///     DEFECT. <c>Field 'OptionalAssociate' required but not present in BsonDocument</c>, which
    ///     is the same family as §1.6: an absent optional owned reference is not treated as null.
    /// </summary>
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_nested_collection_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_nested_collection_on_optional_associate(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <summary>
    ///     The untracked arm reaches the store and the tracked arm fails inside the base's own
    ///     assertion. See the class remarks on why only the first is overridden.
    /// </summary>
    private static Task Untracked(QueryTrackingBehavior queryTrackingBehavior, Func<Task> test)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? test()
            : StoreBehaviour.Refuses(test, nameof(InvalidOperationException));
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectSetOperationsTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     LIMIT. <c>Concat</c> over an owned collection is not translated, in both tracking arms.
    /// </summary>
    /// <remarks>
    ///     Their own suite declares <c>Except</c> and <c>Intersect</c> unsupported and says nothing
    ///     about <c>Concat</c>, and declares those for cross-collection shapes rather than a
    ///     collection nested inside one document. So the evidence is this control and not theirs.
    /// </remarks>
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_associate_collection_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <inheritdoc cref="Over_associate_collection_projected" />
    public override Task Over_assocate_collection_Select_nested_with_aggregates_projected(
        QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_assocate_collection_Select_nested_with_aggregates_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <summary>
    ///     DEFECT. The store builds a pipeline whose <c>$size</c> operand is not an array. See
    ///     <c>docs/upstream-defects.md</c> §1.9, which notes this may be §1.6 in another form.
    /// </summary>
    public override Task Over_different_collection_properties()
        => StoreBehaviour.Refuses(base.Over_different_collection_properties, "MongoCommandException");
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectStructuralEqualityTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     LIMIT. The driver refuses to compare two owned entities as wholes. Untested upstream in
    ///     any shape, so this control is the only evidence.
    /// </summary>
    public override Task Two_associates()
        => StoreBehaviour.Refuses(base.Two_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    public override Task Two_nested_associates()
        => StoreBehaviour.Refuses(base.Two_nested_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    public override Task Not_equals()
        => StoreBehaviour.Refuses(base.Not_equals, "ExpressionNotSupportedException");
}
