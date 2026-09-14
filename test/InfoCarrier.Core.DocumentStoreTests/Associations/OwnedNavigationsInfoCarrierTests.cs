// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     The other five classes of EF's <c>OwnedNavigations</c> family over ADR-009 Tier D.
/// </summary>
/// <remarks>
///     <para>
///         <b>The sixth, <c>OwnedNavigationsCollectionInfoCarrierTest</c>, is in a file of its own</b>
///         because it was adopted first to find out what adopting this family against a document
///         store costs. It carries the family's reasoning; this file is the rest of the family on
///         the shape that one settled.
///     </para>
///     <para>
///         <b>THREE OVERRIDES IN THIS FILE, EACH CARRYING A CITATION, AND EVERYTHING ELSE IS RED.</b>
///         The rule is that a red must say something about THIS provider. If the backing store
///         refuses a query then this provider cannot answer it either, so the red restates the
///         obvious and is noise in the failure list. That argument is only as good as the evidence
///         the store really does refuse, and the evidence has to be THEIRS: a citation to the
///         store's own suite declaring the feature unsupported. Without one, "the store cannot do
///         this" is our assertion about somebody else's code, and the test stays red.
///     </para>
///     <para>
///         <b>Of 38 reds, 7 cases across 4 methods cleared that bar and 31 did not.</b> The reason
///         so few is worth knowing: <c>MongoDB.EntityFrameworkCore</c>'s specification suite maps
///         Northwind as separate COLLECTIONS and contains no owned or embedded collection anywhere,
///         so its tracked issues — EF-X001 "subquery selection" and the rest — are about
///         cross-collection <c>$lookup</c> joins and say nothing about a nested document. Matching
///         them on the NAME of an operator would be a false match: <c>SelectMany</c> across
///         collections is a join, and <c>SelectMany</c> within a document is an <c>$unwind</c>.
///         Their hand-written <c>FunctionalTests/Query/UnsupportedQueryTests.cs</c> is the one
///         place that does use an embedded array, and it is what the overrides below cite.
///     </para>
///     <para>
///         <b>So this tier is measuring nested-document query support that the store's own
///         specification suite has never measured</b>, which is the strongest thing #51 has yet
///         got out of it. Several reds are plausibly unimplemented rather than impossible.
///     </para>
///     <para>
///         <b>The CORE bases, not the relational ones, and that is not a style choice.</b> Tier B
///         adopts <c>OwnedNavigations*RelationalTestBase</c> because SQLite is a relational store.
///         MongoDB is not, so the relational bases' assumptions — tables, a join, golden SQL — have
///         no meaning here. The core base is the part of the family that asks about the model and
///         the query rather than about the statement text.
///     </para>
///     <para>
///         <b>A FIXTURE PER CLASS, BECAUSE ONE SERVER PER TEST CLASS IS THIS TIER'S RULE.</b> xUnit
///         gives each class its own <c>IClassFixture</c> instance, so six classes start six
///         <c>mongod</c> processes whatever their names. Naming each store separately is what keeps
///         a failure readable: the store name is the database name, so a dump says which class the
///         data belonged to.
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
    ///     <c>SelectMany</c> over an owned collection: declared unsupported by the store, so a red
    ///     here would say nothing about the wire.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>THE CITATION IS THE WHOLE JUSTIFICATION FOR THIS OVERRIDE.</b>
    ///         <c>MongoDB.EntityFrameworkCore</c>'s own
    ///         <c>FunctionalTests/Query/UnsupportedQueryTests.cs</c> declares <c>SelectMany</c> over
    ///         a non-primitive collection unsupported and asserts the same
    ///         <c>InvalidOperationException</c>, on an EMBEDDED array
    ///         (<c>p.mainAtmosphere</c>) rather than a cross-collection reference. The shape is
    ///         ours and the verdict is theirs.
    ///     </para>
    ///     <para>
    ///         <b>Why that earns an override where 31 other reds do not.</b> A red is information
    ///         about THIS provider. If the backing store refuses a query, this provider cannot
    ///         answer it either, and the red restates the obvious. The bar is a citation: without
    ///         one, the claim "the store cannot do this" is our own assertion about somebody else's
    ///         code, and it stays red. Their specification suite maps Northwind as separate
    ///         COLLECTIONS and never exercises an owned collection, so its issue numbers — EF-X001
    ///         and the rest — describe cross-collection joins and are not evidence about nested
    ///         documents. <c>UnsupportedQueryTests</c> is, because it uses an embedded array.
    ///     </para>
    ///     <para>
    ///         <b>Both tracking arms fail identically</b>, because the store refuses to translate
    ///         before EF reaches its own rule about tracking an owned entity without its owner.
    ///     </para>
    /// </remarks>
    public override Task SelectMany_associate_collection(QueryTrackingBehavior queryTrackingBehavior)
        => RefusedByTheStore(queryTrackingBehavior, () => base.SelectMany_associate_collection(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_required_associate(QueryTrackingBehavior queryTrackingBehavior)
        => RefusedByTheStore(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_required_associate(queryTrackingBehavior));

    /// <inheritdoc cref="SelectMany_associate_collection" />
    public override Task SelectMany_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => RefusedByTheStore(
            queryTrackingBehavior, () => base.SelectMany_nested_collection_on_optional_associate(queryTrackingBehavior));

    /// <summary>
    ///     The store's refusal reaches the two tracking arms as two different failures, and this is
    ///     the one helper that says so.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Under <c>NoTracking</c> the refusal arrives as a translation failure</b> and
    ///         <c>AssertTranslationFailed</c> is EF's own way to state that — the same call
    ///         <c>MongoDB.EntityFrameworkCore</c>'s suite uses for its unsupported shapes.
    ///     </para>
    ///     <para>
    ///         <b>Under <c>TrackAll</c> the base has already caught the exception and is comparing
    ///         its MESSAGE</b> against EF's rule about tracking an owned entity without its owner,
    ///         a rule the query never reaches because the store refuses first. So the failure that
    ///         escapes is the base's own <c>EqualException</c>, and asserting it is the statement:
    ///         EF wrote the same idiom for SQLite and Tier B adopted it verbatim in
    ///         <c>OwnedNavigationsSetOperationsQueryInfoCarrierTest.Over_associate_collection_projected</c>.
    ///     </para>
    ///     <para>
    ///         <b>One citation covers both arms</b>, because one refusal causes both.
    ///     </para>
    /// </remarks>
    private static Task RefusedByTheStore(QueryTrackingBehavior queryTrackingBehavior, Func<Task> test)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? Assert.ThrowsAsync<EqualException>(test)
            : AssertTranslationFailed(test);

    /// <summary>
    ///     A subquery in a projection: the store refuses to translate it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>CITED TO THE CONTROL, NOT UPSTREAM, WHICH IS A WEAKER CLAIM AND SHOULD READ AS
    ///         ONE.</b> <c>OwnedNavigationsDirectStoreFixture</c> runs this shape on plain EF Core
    ///         over the same MongoDB and gets the identical refusal, so the wire is not the cause.
    ///         Their own suite says nothing about it: EF-X001 "subquery selection" covers
    ///         subqueries ACROSS COLLECTIONS, which on a document store is a join, and this is a
    ///         subquery WITHIN one document. Matching those on the operator name would be a false
    ///         match.
    ///     </para>
    ///     <para>
    ///         <b>Only the untracked arm is overridden.</b> Under <c>TrackAll</c> the base catches
    ///         the exception and compares its message, so what escapes is xUnit's assertion failure
    ///         rather than the store's refusal. An override there would assert almost nothing and
    ///         would stay green if the store began returning wrong rows, so that arm stays RED.
    ///     </para>
    /// </remarks>
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" />
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior),
                nameof(InvalidOperationException));

    /// <summary>
    ///     Projecting an UNMAPPED property of an owned entity: the driver refuses by name.
    /// </summary>
    /// <inheritdoc cref="Select_subquery_required_related_FirstOrDefault" path="/remarks" />
    public override Task Select_unmapped_associate_scalar_property(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? base.Select_unmapped_associate_scalar_property(queryTrackingBehavior)
            : StoreBehaviour.Refuses(
                () => base.Select_unmapped_associate_scalar_property(queryTrackingBehavior),
                "ExpressionNotSupportedException");

    /// <summary>
    ///     A navigation from one ROOT to another, which a document store has no join to resolve.
    /// </summary>
    /// <remarks>
    ///     <b>THE TWO ARMS FAIL FOR DIFFERENT REASONS AND ONLY ONE IS A REFUSAL.</b> Under
    ///     <c>TrackAll</c> EF states its own rule about tracking an owned entity without its owner
    ///     and the query stops there; that is a refusal and is overridden. Under <c>NoTracking</c>
    ///     the store raises a <c>NullReferenceException</c> - a CRASH rather than a refusal,
    ///     reproduced by the control without the wire, and left RED because a store that means "no"
    ///     says so. It is one of this tier's candidate upstream defects.
    /// </remarks>
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior is QueryTrackingBehavior.TrackAll
            ? StoreBehaviour.Refuses(
                () => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior),
                nameof(InvalidOperationException))
            : base.Select_required_associate_via_optional_navigation(queryTrackingBehavior);
}

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsSetOperationsInfoCarrierTest(OwnedNavigationsSetOperationsFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsSetOperationsFixture>(fixture)
{
    /// <summary>
    ///     <c>Concat</c> over an owned collection: the store refuses to translate it, in both
    ///     tracking arms.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>CITED TO THE CONTROL, NOT UPSTREAM.</b> The wire-free control gets the identical
    ///         refusal, so this is not the wire. Their own suite declares <c>Except</c> and
    ///         <c>Intersect</c> unsupported and says nothing about <c>Concat</c> - and declares
    ///         those for CROSS-COLLECTION shapes rather than a collection nested inside one
    ///         document, so there is no upstream citation to take and this is our own measurement.
    ///     </para>
    ///     <para>
    ///         <c>Over_different_collection_properties</c> is NOT overridden: it fails with the
    ///         store's <c>$size must be an array</c>, a crash rather than a refusal, and stays red
    ///         as a candidate upstream defect.
    ///     </para>
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
}

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsStructuralEqualityInfoCarrierTest(OwnedNavigationsStructuralEqualityFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsStructuralEqualityFixture>(fixture)
{
    /// <summary>
    ///     Comparing two owned entities as wholes: the driver refuses by name.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>CITED TO THE CONTROL, NOT UPSTREAM.</b> Structural equality over owned entities
    ///         is untested in <c>MongoDB.EntityFrameworkCore</c>'s suite in any shape, so there is
    ///         nothing to cite but our own measurement: the wire-free control raises the identical
    ///         <c>ExpressionNotSupportedException</c>.
    ///     </para>
    ///     <para>
    ///         The four <c>Nested_*_with_inline</c> and <c>_with_parameter</c> tests here are NOT
    ///         overridden. Two fail inside the base's own assertion, so an override would assert
    ///         that the assertion failed and nothing more. The other two fail because the store
    ///         ANSWERS a query EF's base expects it to refuse (EF #36400), and the control shows
    ///         the answer is CORRECT. Both are more informative as red.
    ///     </para>
    /// </remarks>
    public override Task Two_associates()
        => StoreBehaviour.Refuses(base.Two_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    public override Task Two_nested_associates()
        => StoreBehaviour.Refuses(base.Two_nested_associates, "ExpressionNotSupportedException");

    /// <inheritdoc cref="Two_associates" />
    public override Task Not_equals()
        => StoreBehaviour.Refuses(base.Not_equals, "ExpressionNotSupportedException");

    /// <summary>
    ///     EF expects this to be refused (#36400). This store ANSWERS it, and the answer is right.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>THE QUERY IS REWRITTEN HERE RATHER THAN SUPPRESSED, AND THAT IS THE WHOLE
    ///         POINT.</b> <c>OwnedNavigationsStructuralEqualityTestBase</c> wraps this in
    ///         <c>Assert.ThrowsAsync&lt;InvalidOperationException&gt;</c> for EF issue #36400, so
    ///         when nothing throws, the base never compares the rows and the failure that escapes
    ///         is xUnit's own. An override asserting THAT would stay green whether this store
    ///         returned the right row, the wrong row, or none at all.
    ///     </para>
    ///     <para>
    ///         So this asserts what the seed data implies: exactly one root carries that nested
    ///         associate. EF's expectation was wrong for this store, and the right answer is
    ///         written down instead of the disagreement being hidden.
    ///     </para>
    /// </remarks>
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
    /// <remarks>
    ///     The same shape through a captured variable rather than an inline initializer, which is
    ///     the only thing the base varies between the two.
    /// </remarks>
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
    ///     Comparing a whole owned COLLECTION: this store refuses, with its own exception type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Rewritten rather than suppressed, for the same reason as
    ///         <see cref="Nested_associate_with_inline" />.</b> The base expects EF's
    ///         <c>InvalidOperationException</c> (#36400) and this store raises
    ///         <c>NotSupportedException</c> instead, so the base's own assertion is what fails and
    ///         catching THAT would assert almost nothing. Asserting the store's exception directly
    ///         means the test goes red the day the store answers, or refuses differently.
    ///     </para>
    ///     <para>
    ///         Cited to the control, not upstream: the wire-free run raises the identical
    ///         exception, and MongoDB's own suite exercises no owned collection at all.
    ///     </para>
    /// </remarks>
    public override async Task Nested_collection_with_inline()
    {
        using DbContext context = Fixture.CreateContext();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Set<RootEntity>()
                .AsNoTracking()
                .Where(e => e.RequiredAssociate.NestedCollection
                    == new List<NestedAssociateType>
                    {
                        new()
                        {
                            Id = 1002,
                            Name = "Root1_RequiredRelated_NestedCollection_1",
                            Int = 8,
                            String = "foo",
                            Ints = new List<int> { 1, 2, 3 },
                        },
                        new()
                        {
                            Id = 1003,
                            Name = "Root1_RequiredRelated_NestedCollection_2",
                            Int = 8,
                            String = "foo",
                            Ints = new List<int> { 1, 2, 3 },
                        },
                    })
                .ToListAsync());
    }

    /// <inheritdoc cref="Nested_collection_with_inline" />
    public override async Task Nested_collection_with_parameter()
    {
        using DbContext context = Fixture.CreateContext();

        var nested = new List<NestedAssociateType>
        {
            new()
            {
                Id = 1002,
                Name = "Root1_RequiredRelated_NestedCollection_1",
                Int = 8,
                String = "foo",
                Ints = [1, 2, 3],
            },
            new()
            {
                Id = 1003,
                Name = "Root1_RequiredRelated_NestedCollection_2",
                Int = 8,
                String = "foo",
                Ints = [1, 2, 3],
            },
        };

        await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Set<RootEntity>()
                .AsNoTracking()
                .Where(e => e.RequiredAssociate.NestedCollection == nested)
                .ToListAsync());
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
