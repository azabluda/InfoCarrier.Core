// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The other five classes of EF's <c>OwnedNavigations</c> family over ADR-009 Tier D.
/// </summary>
/// <remarks>
///     <para>
///         <b>The sixth, <c>OwnedNavigationsCollectionInfoCarrierTest</c>, is in a file of its own</b>
///         because it was adopted first to find out what this family costs against a document store.
///     </para>
///     <para>
///         <b>EVERY OVERRIDE SAYS WHAT THE STORE DOES AND WHERE THAT IS SHOWN, AND NONE IS A
///         SKIP.</b> The attribute's type is the label — <c>LIMIT</c>, <c>DEFECT</c> or
///         <c>ISSUE</c> — and its arguments name the test of a <c>Direct*</c> control that shows the
///         same behaviour with InfoCarrier removed. <c>OverrideAudit</c> fails the build when an
///         override has no reason or disagrees with its control. <c>docs/plans/v10/test-overhaul.md</c>
///         is the reading.
///     </para>
///     <para>
///         <b>Every reference here is self-hosted, and that is a finding about the store.</b> The
///         first classification cited MongoDB's <c>UnsupportedQueryTests.cs</c> for
///         <c>SelectMany</c>, calling its test "an embedded array, which is our shape". At the
///         <c>v10.0.3</c> tag both of those tests select from <c>string[]</c>, a primitive
///         collection, and this family selects from owned entities. MongoDB's suite has not tested
///         queries over nested documents, so this tier measures them itself.
///     </para>
///     <para>
///         <b>No InfoCarrier defect is overridden here, because there is none.</b> Every behaviour
///         below is reproduced by the control with the wire removed. The two cases where this
///         provider does better than the raw store need no override at all:
///         <c>Select_untranslatable_method_on_associate_scalar_property</c> passes here and fails
///         on MongoDB alone, because the projection split evaluates the method on the client.
///     </para>
///     <para>
///         <b>The CORE bases, not the relational ones.</b> Tier B adopts
///         <c>OwnedNavigations*RelationalTestBase</c> because SQLite is relational; MongoDB is not, so
///         golden SQL and joins have no meaning here.
///     </para>
///     <para>
///         <b>A fixture per class, because one server per test class is this tier's rule.</b> The
///         store name is the database name, so a failure says which class the data belonged to.
///     </para>
/// </remarks>
public class OwnedNavigationsMiscellaneousInfoCarrierTest(OwnedNavigationsMiscellaneousFixture fixture)
    : OwnedNavigationsMiscellaneousTestBase<OwnedNavigationsMiscellaneousFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsPrimitiveCollectionInfoCarrierTest(OwnedNavigationsPrimitiveCollectionFixture fixture)
    : OwnedNavigationsPrimitiveCollectionTestBase<OwnedNavigationsPrimitiveCollectionFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsProjectionInfoCarrierTest(OwnedNavigationsProjectionFixture fixture)
    : OwnedNavigationsProjectionTestBase<OwnedNavigationsProjectionFixture>(fixture)
{
    /// <summary>
    ///     The prefix of EF's translation failure, which survives xUnit shortening the message.
    /// </summary>
    private const string NotTranslated = "The LINQ expression 'DbSet<RootEntity>()";

    /// <summary><c>SelectMany</c> over an owned collection does not translate, in either arm.</summary>
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.SelectMany_associate_collection))]
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(queryTrackingBehavior, () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.SelectMany_nested_collection_on_required_associate))]
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.SelectMany_nested_collection_on_optional_associate))]
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <summary>A subquery inside a projection does not translate, in either arm.</summary>
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_subquery_required_related_FirstOrDefault))]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_subquery_optional_related_FirstOrDefault))]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_subquery_FirstOrDefault_complex_collection))]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => NotTranslatedInEitherArm(
            queryTrackingBehavior, () => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));

    /// <summary>The driver refuses an unmapped property by name, in either arm.</summary>
    [StoreLimit(typeof(DirectProjectionTest), nameof(DirectProjectionTest.Select_unmapped_associate_scalar_property))]
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
    ///     A navigation from one root to another: a refusal when tracked, a crash when not.
    /// </summary>
    [StoreLimit(
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_required_associate_via_optional_navigation),
        Case = nameof(QueryTrackingBehavior.TrackAll))]
    [StoreDefect(
        "1.6",
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_required_associate_via_optional_navigation),
        Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior),
            queryTrackingBehavior is QueryTrackingBehavior.TrackAll
                ? nameof(InvalidOperationException)
                : nameof(NullReferenceException));

    /// <summary>Projecting through a null optional owned reference crashes. The tracked arm passes.</summary>
    [StoreDefect(
        "1.6",
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_required_nested_on_optional_associate),
        Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_required_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_required_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_required_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <inheritdoc cref="Select_required_nested_on_optional_associate" />
    [StoreDefect(
        "1.6",
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_optional_nested_on_optional_associate),
        Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_optional_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_optional_nested_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_optional_nested_on_optional_associate(queryTrackingBehavior),
                nameof(NullReferenceException));

    /// <summary>An absent optional owned reference is not treated as null. The tracked arm passes.</summary>
    [StoreDefect(
        "1.6",
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_nested_collection_on_optional_associate),
        Case = nameof(QueryTrackingBehavior.NoTracking))]
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_nested_collection_on_optional_associate(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_nested_collection_on_optional_associate(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <summary>
    ///     Reading a value-type property through a null optional owned reference crashes, in both arms.
    /// </summary>
    [StoreDefect(
        "1.6",
        typeof(DirectProjectionTest),
        nameof(DirectProjectionTest.Select_value_type_property_on_null_associate_throws))]
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

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsSetOperationsInfoCarrierTest(OwnedNavigationsSetOperationsFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsSetOperationsFixture>(fixture)
{
    /// <summary><c>Concat</c> over an owned collection does not translate, in either arm.</summary>
    [StoreLimit(typeof(DirectSetOperationsTest), nameof(DirectSetOperationsTest.Over_associate_collection_projected))]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_associate_collection_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <inheritdoc cref="Over_associate_collection_projected" />
    [StoreLimit(
        typeof(DirectSetOperationsTest),
        nameof(DirectSetOperationsTest.Over_assocate_collection_Select_nested_with_aggregates_projected))]
    public override Task Over_assocate_collection_Select_nested_with_aggregates_projected(
        QueryTrackingBehavior queryTrackingBehavior)
        => StoreBehaviour.Refuses(
            () => base.Over_assocate_collection_Select_nested_with_aggregates_projected(queryTrackingBehavior),
            nameof(InvalidOperationException));

    /// <summary>The store builds a pipeline whose <c>$size</c> operand is not an array.</summary>
    [StoreDefect(
        "1.9",
        typeof(DirectSetOperationsTest),
        nameof(DirectSetOperationsTest.Over_different_collection_properties),
        Deviation = "Over the wire the store's MongoCommandException arrives wrapped as InfoCarrierServerException, "
            + "so the store's own text is asserted as well, because that type wraps every server failure.")]
    public override Task Over_different_collection_properties()
        => StoreBehaviour.Refuses(
            base.Over_different_collection_properties,
            "InfoCarrierServerException",
            "The argument to $size must be an array");
}

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsStructuralEqualityInfoCarrierTest(OwnedNavigationsStructuralEqualityFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsStructuralEqualityFixture>(fixture)
{
    /// <summary>The driver refuses to compare two owned entities as wholes.</summary>
    [StoreLimit(typeof(DirectStructuralEqualityTest), nameof(DirectStructuralEqualityTest.Two_associates))]
    public override Task Two_associates()
        => StoreBehaviour.Refuses(base.Two_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    [StoreLimit(typeof(DirectStructuralEqualityTest), nameof(DirectStructuralEqualityTest.Two_nested_associates))]
    public override Task Two_nested_associates()
        => StoreBehaviour.Refuses(base.Two_nested_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    [StoreLimit(typeof(DirectStructuralEqualityTest), nameof(DirectStructuralEqualityTest.Not_equals))]
    public override Task Not_equals()
        => StoreBehaviour.Refuses(base.Not_equals, "ExpressionNotSupportedException");

    /// <summary>Comparing a whole owned collection is refused with the store's own exception.</summary>
    [StoreLimit(typeof(DirectStructuralEqualityTest), nameof(DirectStructuralEqualityTest.Nested_collection_with_inline))]
    public override Task Nested_collection_with_inline()
        => StoreBehaviour.BaseExpectedAnotherException(
            base.Nested_collection_with_inline, nameof(NotSupportedException), "Entity to entity comparison is not supported");

    /// <inheritdoc cref="Nested_collection_with_inline" />
    [StoreLimit(typeof(DirectStructuralEqualityTest), nameof(DirectStructuralEqualityTest.Nested_collection_with_parameter))]
    public override Task Nested_collection_with_parameter()
        => StoreBehaviour.BaseExpectedAnotherException(
            base.Nested_collection_with_parameter, nameof(NotSupportedException), "Entity to entity comparison is not supported");

    /// <summary>
    ///     EF's base expects a refusal (<c>dotnet/efcore#36400</c>). This store ANSWERS, correctly.
    /// </summary>
    [StoreIssue(
        "dotnet/efcore#36400",
        typeof(DirectStructuralEqualityTest),
        nameof(DirectStructuralEqualityTest.Nested_associate_with_inline),
        Deviation = DirectStructuralEqualityTest.WrittenOut)]
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
    [StoreIssue(
        "dotnet/efcore#36400",
        typeof(DirectStructuralEqualityTest),
        nameof(DirectStructuralEqualityTest.Nested_associate_with_parameter),
        Deviation = DirectStructuralEqualityTest.WrittenOut)]
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
}

/// <summary>A store of its own for <see cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />.</summary>
public sealed class OwnedNavigationsMiscellaneousFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsMiscellaneous";
}

/// <summary>A store of its own for <see cref="OwnedNavigationsPrimitiveCollectionInfoCarrierTest" />.</summary>
public sealed class OwnedNavigationsPrimitiveCollectionFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsPrimitiveCollection";
}

/// <summary>A store of its own for <see cref="OwnedNavigationsProjectionInfoCarrierTest" />.</summary>
public sealed class OwnedNavigationsProjectionFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsProjection";
}

/// <summary>A store of its own for <see cref="OwnedNavigationsSetOperationsInfoCarrierTest" />.</summary>
public sealed class OwnedNavigationsSetOperationsFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsSetOperations";
}

/// <summary>A store of its own for <see cref="OwnedNavigationsStructuralEqualityInfoCarrierTest" />.</summary>
public sealed class OwnedNavigationsStructuralEqualityFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsStructuralEquality";
}
