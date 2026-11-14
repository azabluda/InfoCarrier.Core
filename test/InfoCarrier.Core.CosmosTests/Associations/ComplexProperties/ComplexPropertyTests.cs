// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Associations.ComplexProperties;

public class MiscellaneousTest(ComplexFixture fixture) : ComplexPropertiesMiscellaneousTestBase<ComplexFixture>(fixture);

public class CollectionTest(ComplexFixture fixture) : ComplexPropertiesCollectionTestBase<ComplexFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 30, 46,
        Justification = "Aggregates over subqueries return null result set", Skip = true)]
    public override Task Where()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 209, 227,
        Justification = "Aggregates over subqueries return null result set", Skip = true)]
    public override Task Select_within_Select_within_Select_with_aggregates()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 109, 112,
        Justification = Upstream.GaveNoReason)]
    public override Task Distinct()
        => AssertTranslationFailed(base.Distinct);

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 128, 129,
        Justification = Upstream.GaveNoReason)]
    public override Task Distinct_over_projected_nested_collection()
        => AssertTranslationFailed(base.Distinct_over_projected_nested_collection);

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 131, 132,
        Justification = Upstream.GaveNoReason)]
    public override Task Distinct_over_projected_filtered_nested_collection()
        => AssertTranslationFailed(base.Distinct_over_projected_filtered_nested_collection);

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 201, 207,
        Justification = Upstream.GaveNoReason)]
    [Fact]
    public override Task GroupBy()
        => AssertTranslationFailed(base.GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 93, 107,
        Justification = "'ORDER BY' is not supported in subqueries.", Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "The wire represents CosmosException as InfoCarrierServerException.")]
    public override async Task OrderBy_ElementAt()
    {
        InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(base.OrderBy_ElementAt);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("'ORDER BY' is not supported in subqueries.", error.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 164, 175,
        Justification = "The specified query includes 'member indexer' which is currently not supported", Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "The wire represents CosmosException as InfoCarrierServerException.")]
    public override async Task Index_column()
    {
        InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Index_column);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("'member indexer'", error.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesCollectionCosmosTest.cs", 229, 231,
        Justification = "Cosmos doesn't support entity collection navigations across documents", Skip = true)]
    public override Task Project_struct_complex_type_with_entity_collection_navigation()
        => throw SkipException.ForSkip("Cosmos doesn't support entity collection navigations across documents");

    // Additional official Cosmos scenario, same pinned source, lines 48-91.
    [Fact]
    public async Task Where_subquery_structural_equality()
    {
        var param = new AssociateType
        {
            Id = 1,
            Name = "Name 1",
            Int = 8,
            String = "String 1",
            Ints = [1, 2, 3],
            RequiredNestedAssociate = new NestedAssociateType
            {
                Id = 1,
                Name = "Name 1",
                Int = 8,
                String = "String 1",
                Ints = [1, 2, 3]
            },
            NestedCollection =
            [
                new NestedAssociateType
                {
                    Id = 1,
                    Name = "Name 1",
                    Int = 8,
                    String = "String 1",
                    Ints = [1, 2, 3]
                }
            ]
        };

        await AssertQuery(
            ss => ss.Set<RootEntity>().Where(e => e.AssociateCollection[0] != param),
            ss => ss.Set<RootEntity>().Where(e => e.AssociateCollection.Count > 0 && e.AssociateCollection[0] != param));
    }
}

public class PrimitiveCollectionTest(ComplexFixture fixture) : ComplexPropertiesPrimitiveCollectionTestBase<ComplexFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesPrimitiveCollectionCosmosTest.cs", 76, 93,
        Justification = "Aggregates over subqueries return null result set", Skip = true)]
    public override Task Select_Sum()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    // Additional official Cosmos scenario, same pinned source, lines 95-120.
    [Fact]
    public virtual async Task Where_constant()
    {
        await AssertQuery(
            ss => ss.Set<RootEntity>().Where(e => e.RequiredAssociate.Ints
            == new List<int>
            {
                1,
                2,
                3
            }),
            ss => ss.Set<RootEntity>().Where(e => e.RequiredAssociate.Ints.SequenceEqual(
                new List<int>
                {
                    1,
                    2,
                    3
                })));
    }
}

public class SetOperationsTest(ComplexFixture fixture) : ComplexPropertiesSetOperationsTestBase<ComplexFixture>(fixture)
{
    [StoreIssue(IssueTracker.EfCore, 37918, UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesSetOperationsCosmosTest.cs", 34, 35,
        Justification = "#37918")]
    public override Task Over_associate_collection_projected(QueryTrackingBehavior queryTrackingBehavior)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Over_associate_collection_projected(queryTrackingBehavior)); // #37918

    [StoreDefect("2.1", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesSetOperationsCosmosTest.cs", 37, 58,
        Justification = Upstream.GaveNoReason, Skip = true)]
    public override Task Over_assocate_collection_Select_nested_with_aggregates_projected(QueryTrackingBehavior queryTrackingBehavior)
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesSetOperationsCosmosTest.cs", 78, 79,
        Justification = Upstream.GaveNoReason)]
    public override Task Over_different_collection_properties()
        => AssertTranslationFailed(base.Over_different_collection_properties);

}

public class StructuralEqualityTest(ComplexFixture fixture) : ComplexPropertiesStructuralEqualityTestBase<ComplexFixture>(fixture)
{
    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 87, 100,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Nested_associate_with_inline()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 102, 117,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Nested_associate_with_parameter()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 163, 176,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Nested_collection_with_inline()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 178, 193,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Nested_collection_with_parameter()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 208, 226,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Contains_with_inline()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 228, 246,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Contains_with_parameter()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 248, 267,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Contains_with_operators_composed_on_the_collection()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.2", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesStructuralEqualityCosmosTest.cs", 269, 288,
        Justification = "Complex-type equality comparisons return no results", Skip = true)]
    public override Task Contains_with_nested_and_composed_operators()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    // Additional official Cosmos scenario, same pinned source, lines 119-133.
    [Fact]
    public async Task Nested_associate_with_parameter_null()
    {
        NestedAssociateType? nested = null;
        await AssertQuery(ss => ss.Set<RootEntity>().Where(e => e.RequiredAssociate.OptionalNestedAssociate == nested));
    }

    // Additional official Cosmos scenario, same pinned source, lines 135-149.
    [Fact]
    public async Task Nested_associate_with_parameter_not_null()
    {
        NestedAssociateType? nested = null;
        await AssertQuery(ss => ss.Set<RootEntity>().Where(e => e.RequiredAssociate.OptionalNestedAssociate != nested));
    }

}

public class ProjectionTest(ComplexFixture fixture) : ComplexPropertiesProjectionTestBase<ComplexFixture>(fixture)
{
    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 39, 53,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.")]
    public override async Task Select_property_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.String),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.String),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 55, 69,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.")]
    public override async Task Select_value_type_property_on_null_associate_throws(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.Int),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.Int),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 71, 85,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.")]
    public override async Task Select_nullable_value_type_property_on_null_associate(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => (int?)x.OptionalAssociate!.Int),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => (int?)x.OptionalAssociate!.Int),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 256, 274,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.")]
    public override async Task Select_nested_collection_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().OrderBy(e => e.Id).Select(x => x.OptionalAssociate!.NestedCollection),
            ss => ss.Set<RootEntity>().OrderBy(e => e.Id).Where(x => x.OptionalAssociate != null)
                .Select(x => x.OptionalAssociate!.NestedCollection),
            assertOrder: true,
            elementAsserter: (e, a) => AssertCollection(e, a, elementSorter: r => r.Id),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 169, 183,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.", Deviation = DeviationKind.Other,
        DeviationNote = "Use the nested member named by the overridden base; upstream selects its sibling.")]
    public override async Task Select_required_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.RequiredNestedAssociate),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.RequiredNestedAssociate),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 185, 199,
        Justification = "When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the result to be filtered out entirely.", Deviation = DeviationKind.Other,
        DeviationNote = "Use the nested member named by the overridden base; upstream selects its sibling.")]
    public override async Task Select_optional_nested_on_optional_associate(QueryTrackingBehavior queryTrackingBehavior)
    {
        // When OptionalAssociate is null, the property access on it evaluates to undefined in Cosmos, causing the
        // result to be filtered out entirely.
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.OptionalAssociate!.OptionalNestedAssociate),
            ss => ss.Set<RootEntity>().Where(x => x.OptionalAssociate != null).Select(x => x.OptionalAssociate!.OptionalNestedAssociate),
            queryTrackingBehavior: queryTrackingBehavior);
    }

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 201, 204,
        Justification = "We don't support (inter-document) navigations with Cosmos.")]
    public override Task Select_required_associate_via_optional_navigation(QueryTrackingBehavior queryTrackingBehavior)
        // We don't support (inter-document) navigations with Cosmos.
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Select_required_associate_via_optional_navigation(queryTrackingBehavior));

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 382, 387,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => base.Select_subquery_FirstOrDefault_complex_collection(queryTrackingBehavior));

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 389, 390,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => base.Select_subquery_required_related_FirstOrDefault(queryTrackingBehavior));

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 392, 393,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => base.Select_subquery_optional_related_FirstOrDefault(queryTrackingBehavior));

    [StoreLimit(UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 434, 448,
        Justification = Upstream.GaveNoReason)]
    public override async Task Select_nullable_value_type_with_Value(QueryTrackingBehavior queryTrackingBehavior)
    {
        var ex =
            await Assert.ThrowsAsync<InvalidOperationException>(() => base.Select_nullable_value_type_with_Value(queryTrackingBehavior));
        Assert.Equal(
            "Cannot read the Value property of a Nullable object that has no value. Check HasValue before reading Value.",
            ex.Message);
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, "test/EFCore.Cosmos.FunctionalTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionCosmosTest.cs", 364, 380,
        Justification = Upstream.GaveNoReason, Skip = true)]
    public override Task Select_optional_associate_and_ints(QueryTrackingBehavior queryTrackingBehavior)
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    // Additional official Cosmos scenario, same pinned source, lines 87-91.
    [Fact]
    public virtual Task Select_scalar_on_distinct_required_associate()
        => AssertTranslationFailed(() => AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.RequiredAssociate).Distinct().Select(x => x.String),
            queryTrackingBehavior: QueryTrackingBehavior.NoTracking));

    // Additional official Cosmos scenario, same pinned source, lines 108-120.
    [Fact]
    public async Task Select_distinct_associate()
    {
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.RequiredAssociate).Distinct(),
            queryTrackingBehavior: QueryTrackingBehavior.NoTracking);
    }

    // Additional official Cosmos scenario, same pinned source, lines 122-134.
    [Fact]
    public async Task Select_distinct_nested_associate()
    {
        await AssertQuery(
            ss => ss.Set<RootEntity>().Select(x => x.RequiredAssociate.RequiredNestedAssociate).Distinct(),
            queryTrackingBehavior: QueryTrackingBehavior.NoTracking);
    }

}
