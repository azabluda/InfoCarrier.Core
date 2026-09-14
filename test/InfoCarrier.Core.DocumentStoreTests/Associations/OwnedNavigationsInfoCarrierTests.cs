// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
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
}

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsSetOperationsInfoCarrierTest(OwnedNavigationsSetOperationsFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsSetOperationsFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsMiscellaneousInfoCarrierTest" />
public class OwnedNavigationsStructuralEqualityInfoCarrierTest(OwnedNavigationsStructuralEqualityFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsStructuralEqualityFixture>(fixture);

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
