// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Associations;

public abstract class CollectionTests<TFixture>(TFixture fixture) : OwnedNavigationsCollectionTestBase<TFixture>(fixture)
    where TFixture : DocumentAssociationFixture, new()
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Associations/OwnedNavigations/OwnedNavigationsCollectionCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 60, 74, Justification = "'ORDER BY' is not supported in subqueries.",
        Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire represents CosmosException as InfoCarrierServerException.")]
    public override async Task OrderBy_ElementAt()
    {
        Exception error = await Assert.ThrowsAnyAsync<Exception>(base.OrderBy_ElementAt);
        Assert.Equal(Fixture is DirectAssociationFixture ? typeof(Microsoft.Azure.Cosmos.CosmosException) : typeof(InfoCarrierServerException), error.GetType());
        Assert.Contains("'ORDER BY' is not supported in subqueries.", error.Message, StringComparison.Ordinal);
    }
    [StoreLimit(UpstreamRepository.EfCore, Source, 134, 145, Justification = "The specified query includes 'member indexer' which is currently not supported",
        Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire represents CosmosException as InfoCarrierServerException.")]
    public override async Task Index_column()
    {
        Exception error = await Assert.ThrowsAnyAsync<Exception>(base.Index_column);
        Assert.Equal(Fixture is DirectAssociationFixture ? typeof(Microsoft.Azure.Cosmos.CosmosException) : typeof(InfoCarrierServerException), error.GetType());
        Assert.Contains("'member indexer'", error.Message, StringComparison.Ordinal);
    }
    [StoreLimit(UpstreamRepository.EfCore, Source, 76, 79, Justification = Upstream.GaveNoReason)]
    public override Task Distinct() => AssertTranslationFailed(base.Distinct);
    [StoreLimit(UpstreamRepository.EfCore, Source, 98, 99, Justification = Upstream.GaveNoReason)]
    public override Task Distinct_over_projected_nested_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Distinct_over_projected_nested_collection);
    [StoreLimit(UpstreamRepository.EfCore, Source, 101, 102, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other, DeviationNote = "Call the filtered base method; upstream mistakenly calls its unfiltered sibling.")]
    public override Task Distinct_over_projected_filtered_nested_collection()
        => Assert.ThrowsAsync<InvalidOperationException>(base.Distinct_over_projected_filtered_nested_collection);
    [StoreLimit(UpstreamRepository.EfCore, Source, 171, 177, Justification = Upstream.GaveNoReason)]
    public override Task GroupBy() => AssertTranslationFailed(base.GroupBy);
}

public abstract class StructuralEqualityTests<TFixture>(TFixture fixture) : OwnedNavigationsStructuralEqualityTestBase<TFixture>(fixture)
    where TFixture : DocumentAssociationFixture, new()
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Associations/OwnedNavigations/OwnedNavigationsStructuralEqualityCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 63, 64, Justification = Upstream.GaveNoReason)]
    public override Task Associate_with_parameter_null() => Assert.ThrowsAsync<InvalidOperationException>(base.Associate_with_parameter_null);
    [StoreLimit(UpstreamRepository.EfCore, Source, 138, 144, Justification = "No backing field could be found for property 'RootEntity.RequiredRelated#RelatedType.NestedCollection#NestedType.RelatedTypeRootEntityId' and the property does not have a getter.")]
    public override Task Contains_with_inline() => Assert.ThrowsAsync<InvalidOperationException>(base.Contains_with_inline);
    [StoreLimit(UpstreamRepository.EfCore, Source, 146, 151, Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_parameter() => AssertTranslationFailed(base.Contains_with_parameter);
    [StoreLimit(UpstreamRepository.EfCore, Source, 153, 158, Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_operators_composed_on_the_collection() => AssertTranslationFailed(base.Contains_with_operators_composed_on_the_collection);
    [StoreLimit(UpstreamRepository.EfCore, Source, 160, 165, Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_nested_and_composed_operators() => AssertTranslationFailed(base.Contains_with_nested_and_composed_operators);
}

public abstract class SetOperationsTests<TFixture>(TFixture fixture) : OwnedNavigationsSetOperationsTestBase<TFixture>(fixture)
    where TFixture : DocumentAssociationFixture, new()
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Associations/OwnedNavigations/OwnedNavigationsSetOperationsCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 33, 34, Justification = "#37918")]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Over_associate_collection_projected(queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 77, 78, Justification = Upstream.GaveNoReason)]
    public override Task Over_different_collection_properties() => AssertTranslationFailed(base.Over_different_collection_properties);
}

public abstract class ProjectionTests<TFixture>(TFixture fixture) : OwnedNavigationsProjectionTestBase<TFixture>(fixture)
    where TFixture : DocumentAssociationFixture, new()
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Associations/OwnedNavigations/OwnedNavigationsProjectionCosmosTest.cs";
    private const string Undefined = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.";
    [StoreLimit(UpstreamRepository.EfCore, Source, 39, 53, Justification = Undefined)]
    public override Task Select_property_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => AssertQuery(ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.String),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.String),
            queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 55, 69, Justification = Undefined)]
    public override Task Select_value_type_property_on_null_associate_throws(QueryTrackingBehavior queryTrackingBehavior)
        => AssertQuery(ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.Int),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.Int),
            queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 71, 85, Justification = Undefined)]
    public override Task Select_nullable_value_type_property_on_null_associate(QueryTrackingBehavior queryTrackingBehavior)
        => AssertQuery(ss => ss.Set<RootEntity>().Select(x => (int?)x.OptionalAssociate!.Int),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => (int?)x.OptionalAssociate!.Int),
            queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 215, 218, Justification = "We don't support (inter-document) navigations with Cosmos.")]
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 175, 193, Justification = Undefined,
        Deviation = DeviationKind.Other, DeviationNote = "Preserve the required navigation from the base query; upstream swaps the optional and required method bodies.")]
    public override Task Select_required_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_required_nested_on_optional_associate(queryTrackingBehavior)
            : AssertQuery(ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.RequiredNestedAssociate),
                ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.RequiredNestedAssociate),
                queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 195, 213, Justification = Undefined,
        Deviation = DeviationKind.Other, DeviationNote = "Preserve the optional navigation from the base query; upstream swaps the optional and required method bodies.")]
    public override Task Select_optional_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_optional_nested_on_optional_associate(queryTrackingBehavior)
            : AssertQuery(ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.OptionalNestedAssociate),
                ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.OptionalNestedAssociate),
                queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 279, 300, Justification = Undefined)]
    public override Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_nested_collection_on_optional_associate(queryTrackingBehavior)
            : AssertQuery(ss => ss.Set<RootEntity>().OrderBy(e => e.Id).Select(x => x.OptionalAssociate!.NestedCollection),
                ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).OrderBy(e => e.Id).Select(x => x.OptionalAssociate!.NestedCollection),
                assertOrder: true, elementAsserter: (e, a) => AssertCollection(e, a, elementSorter: r => r.Id),
                queryTrackingBehavior: queryTrackingBehavior);
    [StoreLimit(UpstreamRepository.EfCore, Source, 441, 447, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other, DeviationNote = "Run the common base's tracked owned-entity refusal; upstream omits that arm. Keep its non-tracking translation refusal.")]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior)
            : AssertTranslationFailed(() => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 449, 455, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other, DeviationNote = "Run the common base's tracked owned-entity refusal; upstream omits that arm. Keep its non-tracking translation refusal.")]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior)
            : AssertTranslationFailed(() => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 429, 439, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other, DeviationNote = "Run the common base's tracked owned-entity refusal; upstream omits that arm. Keep its non-tracking translation refusal.")]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => queryTrackingBehavior == QueryTrackingBehavior.TrackAll
            ? base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior)
            : AssertTranslationFailed(() => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));
}
