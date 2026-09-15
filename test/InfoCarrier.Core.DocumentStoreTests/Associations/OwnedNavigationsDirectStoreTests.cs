// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's whole <c>OwnedNavigations</c> family against MongoDB with InfoCarrier removed: the
///     evidence every Tier D override refers to.
/// </summary>
/// <remarks>
///     <para>
///         <b>WHY THIS TIER HOSTS ITS OWN EVIDENCE.</b> An override must refer to a place where the
///         store does the same thing, and the first place to look is the store's own suite at the
///         release tag this tier runs. <c>MongoDB.EntityFrameworkCore</c> 10.0.3 has none: its
///         specification suite maps Northwind as separate collections and never queries an owned
///         collection, and its two <c>SelectMany</c> refusals select from <c>string[]</c>, a primitive
///         collection. So the evidence is measured here instead, by running the SAME EF bases, with
///         EF's own queries rather than copies, on plain EF Core over the same embedded MongoDB.
///     </para>
///     <para>
///         <b>THESE CLASSES DOCUMENT THE STORE, SO THEY ARE GREEN.</b> Every override asserts exactly
///         what MongoDB does — its exception, the text inside it, or the value it returns — and
///         carries the label for it. Nothing here is a skip. When the store changes, a test here goes
///         red, and every override that refers to it has to be read again.
///         <c>OverrideAudit</c> checks that each wire override refers to a test here with the same
///         label.
///     </para>
///     <para>
///         <b>A CONTROL IS A CONFLICT OF INTEREST, AND TWO THINGS GUARD THIS ONE.</b> A worse-wired
///         control fails more and makes InfoCarrier look cleaner. First, every assertion names an
///         exact outcome, so a mis-wired control would have to produce the identical exception and
///         text to pass. Second, <see cref="DirectMiscellaneousTest" /> and
///         <see cref="DirectPrimitiveCollectionTest" /> need no override at all and must stay green
///         without one; they are the canary for the wiring. The first version of this control
///         passed <c>null</c> for the model customization, which is the kind of fault they catch.
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
[WireFreeControl]
public class DirectCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     Grouping inside a document is refused by the driver, by name.
    /// </summary>
    /// <remarks>
    ///     <c>LIMIT</c> and not <c>ISSUE EF-149</c>, which the first classification chose. <c>EF-149</c>
    ///     and the upstream <c>GroupBy_cannot_be_translated</c> group the ROOT set and raise
    ///     <c>InvalidOperationException</c>; this groups a collection inside a document and raises
    ///     <c>ExpressionNotSupportedException</c>. A tracker entry has to cover the behaviour to be
    ///     cited, and that one does not.
    /// </remarks>
    [StoreLimit]
    public override Task GroupBy()
        => StoreBehaviour.Refuses(base.GroupBy, "ExpressionNotSupportedException");

    /// <summary>
    ///     <c>Distinct</c> over a projected owned collection does not translate. Under <c>TrackAll</c>
    ///     the base expected EF's own tracking exception and got the store's instead.
    /// </summary>
    [StoreLimit]
    public override Task Distinct_projected(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? StoreBehaviour.BaseExpectedAnotherException(
                () => base.Distinct_projected(queryTrackingBehavior),
                nameof(ArgumentException),
                "cannot be used for parameter of type")
            : StoreBehaviour.Refuses(() => base.Distinct_projected(queryTrackingBehavior), nameof(ArgumentException));

    /// <summary>
    ///     A silent wrong answer: three roots where five are correct. See
    ///     <c>docs/upstream-defects.md</c> §1.7.
    /// </summary>
    [StoreDefect("1.7")]
    public override Task Distinct_over_projected_filtered_nested_collection()
        => StoreBehaviour.BaseExpectedAnotherValue(base.Distinct_over_projected_filtered_nested_collection, 5, 3);

    /// <summary>
    ///     Three nested aggregates collide in the store's own subquery alias table,
    ///     <c>Key: o0</c>. See <c>docs/upstream-defects.md</c> §1.8.
    /// </summary>
    [StoreDefect("1.8")]
    public override Task Select_within_Select_within_Select_with_aggregates()
        => StoreBehaviour.Refuses(base.Select_within_Select_within_Select_with_aggregates, nameof(ArgumentException));
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
[WireFreeControl]
public class DirectMiscellaneousTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsMiscellaneousTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
[WireFreeControl]
public class DirectPrimitiveCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsPrimitiveCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
[WireFreeControl]
public class DirectProjectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsProjectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     The prefix of EF's translation failure, which survives xUnit shortening the message.
    /// </summary>
    private const string NotTranslated = "The LINQ expression 'DbSet<RootEntity>()";

    /// <summary>
    ///     <c>SelectMany</c> over an owned collection does not translate. Under <c>TrackAll</c> the base
    ///     compared the message against EF's own tracking rule, which the query never reaches.
    /// </summary>
    [StoreLimit]
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(queryTrackingBehavior, () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    [StoreLimit]
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    [StoreLimit]
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <summary>A subquery inside a projection does not translate, in either arm.</summary>
    [StoreLimit]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    [StoreLimit]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    [StoreLimit]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));

    /// <summary>The driver refuses an unmapped property by name, in either arm.</summary>
    [StoreLimit]
    public override Task Select_unmapped_associate_scalar_property(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? StoreBehaviour.BaseExpectedAnotherException(
                () => base.Select_unmapped_associate_scalar_property(queryTrackingBehavior),
                "ExpressionNotSupportedException",
                "does not have a member named Unmapped")
            : StoreBehaviour.Refuses(
                () => base.Select_unmapped_associate_scalar_property(queryTrackingBehavior),
                "ExpressionNotSupportedException");

    /// <summary>
    ///     A user-defined static method in the final projection is refused, in both arms.
    /// </summary>
    /// <remarks>
    ///     <c>EF-250</c>, <i>"Allow client evaluation in the final projection"</i>, is Closed and Fixed
    ///     in 10.0.3, which this tier runs. The fix reaches a BCL instance method and not a user static
    ///     one; <see cref="ClientEvaluatedProjectionTest" /> measures both and rules out the
    ///     owned-reference hop. <c>docs/upstream-defects.md</c> §1.10.
    /// </remarks>
    [StoreIssue(IssueTracker.MongoEfCore, 250)]
    public override Task Select_untranslatable_method_on_associate_scalar_property(
        QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Select_untranslatable_method_on_associate_scalar_property(queryTrackingBehavior),
            "ExpressionNotSupportedException");

    /// <summary>
    ///     Two different behaviours in one method, which is why each reason names its case.
    /// </summary>
    /// <remarks>
    ///     Under <c>TrackAll</c> EF's own rule about tracking an owned entity without its owner stops the
    ///     query: a refusal. Under <c>NoTracking</c> the store crashes with
    ///     <c>NullReferenceException</c>, which is <c>docs/upstream-defects.md</c> §1.6.
    /// </remarks>
    [StoreLimit(Case = nameof(QueryTrackingBehavior.TrackAll))]
    [StoreDefect("1.6", Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior),
            queryTrackingBehavior is QueryTrackingBehavior.TrackAll
                ? nameof(InvalidOperationException)
                : nameof(NullReferenceException));

    /// <summary>
    ///     Projecting through a null optional owned reference crashes. <c>docs/upstream-defects.md</c>
    ///     §1.6. The tracked arm passes, so only the untracked arm is changed.
    /// </summary>
    [StoreDefect("1.6", Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_required_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_required_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_required_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <inheritdoc cref="Select_required_nested_on_optional_associate" />
    [StoreDefect("1.6", Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_optional_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_optional_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_optional_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <summary>
    ///     An absent optional owned reference is not treated as null:
    ///     <c>Field 'OptionalAssociate' required but not present in BsonDocument</c>. Same family as
    ///     §1.6. The tracked arm passes.
    /// </summary>
    [StoreDefect("1.6", Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_nested_collection_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_nested_collection_on_optional_associate(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <summary>
    ///     Reading a value-type property through a null optional owned reference crashes, in both arms,
    ///     where the base expects EF's own exception. <c>docs/upstream-defects.md</c> §1.6.
    /// </summary>
    [StoreDefect("1.6")]
    public override Task Select_value_type_property_on_null_associate_throws(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.BaseExpectedAnotherException(
            () => base.Select_value_type_property_on_null_associate_throws(queryTrackingBehavior),
            nameof(NullReferenceException),
            "Object reference not set");

    /// <summary>
    ///     Untracked, the store's translation failure escapes. Tracked, the base compares its message
    ///     against EF's tracking rule, so the store's text is in xUnit's failure instead.
    /// </summary>
    private static Task NotTranslatedInEitherArm(QueryTrackingBehavior queryTrackingBehavior, Func<Task> test)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? StoreBehaviour.BaseExpectedAnotherMessage(test, NotTranslated)
            : StoreBehaviour.Refuses(test, nameof(InvalidOperationException));
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
[WireFreeControl]
public class DirectSetOperationsTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>
    ///     <c>Concat</c> over an owned collection does not translate, in either arm.
    /// </summary>
    /// <remarks>
    ///     Upstream declares <c>Except</c> and <c>Intersect</c> unsupported and says nothing about
    ///     <c>Concat</c>, and does so for root sets rather than a collection inside a document.
    /// </remarks>
    [StoreLimit]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_associate_collection_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <inheritdoc cref="Over_associate_collection_projected" />
    [StoreLimit]
    public override Task Over_assocate_collection_Select_nested_with_aggregates_projected(
        QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_assocate_collection_Select_nested_with_aggregates_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <summary>
    ///     The store builds a pipeline whose <c>$size</c> operand is not an array.
    ///     <c>docs/upstream-defects.md</c> §1.9, which may be §1.6 in another form.
    /// </summary>
    [StoreDefect("1.9")]
    public override Task Over_different_collection_properties()
        => StoreBehaviour.Refuses(base.Over_different_collection_properties, "MongoCommandException");
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
[WireFreeControl]
public class DirectStructuralEqualityTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsDirectStoreFixture>(fixture)
{
    /// <summary>The driver refuses to compare two owned entities as wholes.</summary>
    [StoreLimit]
    public override Task Two_associates()
        => StoreBehaviour.Refuses(base.Two_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    [StoreLimit]
    public override Task Two_nested_associates()
        => StoreBehaviour.Refuses(base.Two_nested_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    [StoreLimit]
    public override Task Not_equals()
        => StoreBehaviour.Refuses(base.Not_equals, "ExpressionNotSupportedException");

    /// <summary>
    ///     Comparing a whole owned collection is refused with the store's own exception, where EF's base
    ///     expects its own.
    /// </summary>
    [StoreLimit]
    public override Task Nested_collection_with_inline()
        => StoreBehaviour.BaseExpectedAnotherException(
            base.Nested_collection_with_inline, nameof(NotSupportedException), "Entity to entity comparison is not supported");

    /// <inheritdoc cref="Nested_collection_with_inline" />
    [StoreLimit]
    public override Task Nested_collection_with_parameter()
        => StoreBehaviour.BaseExpectedAnotherException(
            base.Nested_collection_with_parameter, nameof(NotSupportedException), "Entity to entity comparison is not supported");

    /// <summary>
    ///     EF's base expects a refusal (<c>dotnet/efcore#36400</c>). This store ANSWERS, correctly.
    /// </summary>
    [StoreIssue(IssueTracker.EfCore, 36400, Deviation = DeviationKind.QueryWrittenOut, DeviationNote = WrittenOut)]
    public override async Task Nested_associate_with_inline()
    {
        using DbContext context = Fixture.CreateContext();

        List<RootEntity> result = await context.Set<RootEntity>()
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

    /// <inheritdoc cref="Nested_associate_with_inline" />
    [StoreIssue(IssueTracker.EfCore, 36400, Deviation = DeviationKind.QueryWrittenOut, DeviationNote = WrittenOut)]
    public override async Task Nested_associate_with_parameter()
    {
        using DbContext context = Fixture.CreateContext();

        var nested = new NestedAssociateType
        {
            Id = 1000,
            Name = "Root1_RequiredAssociate_RequiredNestedAssociate",
            Int = 8,
            String = "foo",
            Ints = [1, 2, 3],
        };

        List<RootEntity> result = await context.Set<RootEntity>()
            .AsNoTracking()
            .Where(e => e.RequiredAssociate.RequiredNestedAssociate == nested)
            .ToListAsync();

        Assert.Single(result);
    }

    /// <summary>
    ///     Why two tests here write EF's query out instead of calling the base.
    /// </summary>
    internal const string WrittenOut =
        "EF's base wraps the query in Assert.ThrowsAsync and never compares rows when nothing throws, "
        + "so the query is written out to assert the one row the seed data implies.";
}
