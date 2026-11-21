// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications;

#pragma warning disable EF1001 // Assert pinned provider diagnostic resources and mappings.
public class OwnedQueryTest(OwnedQueryTest.CosmosFixture fixture)
    : OwnedQueryTestBase<OwnedQueryTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/OwnedQueryCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 19, 26, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_on_owned_reference_followed_by_regular_entity_and_collection(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_on_owned_reference_followed_by_regular_entity_and_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 36, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.UpstreamCallsAnotherTest, DeviationNote = "Call the named Fink/Barton scenario; upstream calls an unrelated OwnedPerson/Planet sibling.")]
    public override async Task Query_loads_reference_nav_automatically_in_projection(bool async)
    {
        // Fink.Barton is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Query_loads_reference_nav_automatically_in_projection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Barton", "Fink"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 38, 45, Justification = Upstream.GaveNoReason)]
    public override async Task Query_with_owned_entity_equality_operator(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Query_with_owned_entity_equality_operator(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 47, 61, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Navigation_rewrite_on_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Navigation_rewrite_on_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 63, 85, Skip = true, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override async Task Navigation_rewrite_on_owned_collection_with_composition(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception =
                await AssertCosmosExceptionAsync(() => base.Navigation_rewrite_on_owned_collection_with_composition(async));

            Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 87, 98, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_collection_with_composition_complex(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            // TODO: #33995
            await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Navigation_rewrite_on_owned_collection_with_composition_complex(async));


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 100, 112, Justification = Upstream.GaveNoReason)]
    public override Task Navigation_rewrite_on_owned_reference_projecting_entity(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Navigation_rewrite_on_owned_reference_projecting_entity(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 114, 126, Justification = Upstream.GaveNoReason)]
    public override Task Navigation_rewrite_on_owned_reference_projecting_scalar(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Navigation_rewrite_on_owned_reference_projecting_scalar(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 128, 140, Justification = Upstream.GaveNoReason)]
    public override Task Query_for_base_type_loads_all_owned_navs(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_for_base_type_loads_all_owned_navs(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 142, 154, Justification = Upstream.GaveNoReason)]
    public override Task Query_for_branch_type_loads_all_owned_navs(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_for_branch_type_loads_all_owned_navs(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 156, 168, Justification = Upstream.GaveNoReason)]
    public override Task Query_for_branch_type_loads_all_owned_navs_tracking(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_for_branch_type_loads_all_owned_navs_tracking(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 170, 182, Justification = Upstream.GaveNoReason)]
    public override Task Query_for_leaf_type_loads_all_owned_navs(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_for_leaf_type_loads_all_owned_navs(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 184, 192, Justification = Upstream.GaveNoReason)]
    public override async Task Filter_owned_entity_chained_with_regular_entity_followed_by_projecting_owned_collection(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Filter_owned_entity_chained_with_regular_entity_followed_by_projecting_owned_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 194, 199, Justification = Upstream.GaveNoReason)]
    public override async Task Set_throws_for_owned_type(bool async)
    {
        await base.Set_throws_for_owned_type(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 201, 209, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 211, 219, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_filter(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_filter(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 221, 229, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 231, 239, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference_and_scalar(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference_and_scalar(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 241, 249, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_collection(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 251, 259, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_collection_count(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_collection_count(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 261, 269, Justification = Upstream.GaveNoReason)]
    public override async Task Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_property(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_property(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 271, 282, Justification = Upstream.GaveNoReason)]
    public override async Task
        Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference_in_predicate_and_projection(
            bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Navigation_rewrite_on_owned_reference_followed_by_regular_entity_and_another_reference_in_predicate_and_projection(
                async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 284, 292, Justification = Upstream.GaveNoReason)]
    public override async Task Project_multiple_owned_navigations(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Project_multiple_owned_navigations(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 294, 302, Justification = Upstream.GaveNoReason)]
    public override async Task Project_multiple_owned_navigations_with_expansion_on_owned_collections(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.Project_multiple_owned_navigations_with_expansion_on_owned_collections(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Planet", "OwnedPerson"));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 304, 318, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_on_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.SelectMany_on_owned_collection(a);


            });

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 320, 343, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task SelectMany_with_result_selector(bool async)
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        return NoSyncTest(
            async, async a =>
            {
                await base.SelectMany_with_result_selector(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 345, 354, Justification = Upstream.GaveNoReason)]
    // Address.Planet is a non-owned navigation, cross-document join
    public override async Task SelectMany_on_owned_reference_with_entity_in_between_ending_in_owned_collection(bool async)
    {
        // Address.Planet is a non-owned navigation, cross-document join
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_on_owned_reference_with_entity_in_between_ending_in_owned_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(Planet), nameof(OwnedPerson)));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 356, 358, Justification = Upstream.GaveNoReason)]
    // Non-correlated queries not supported by Cosmos
    public override Task Query_with_owned_entity_equality_method(bool async)
        => AssertTranslationFailed(() => base.Query_with_owned_entity_equality_method(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 360, 362, Justification = Upstream.GaveNoReason)]
    // Non-correlated queries not supported by Cosmos
    public override Task Query_with_owned_entity_equality_object_method(bool async)
        => AssertTranslationFailed(() => base.Query_with_owned_entity_equality_object_method(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 364, 376, Justification = Upstream.GaveNoReason)]
    public override Task Query_with_OfType_eagerly_loads_correct_owned_navigations(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_with_OfType_eagerly_loads_correct_owned_navigations(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 378, 380, Justification = Upstream.GaveNoReason)]
    // TODO: Subquery pushdown, #33968
    public override Task Query_when_subquery(bool async)
        => AssertTranslationFailed(() => base.Query_when_subquery(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 382, 395, Justification = Upstream.GaveNoReason)]
    public override Task Project_owned_reference_navigation_which_owns_additional(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Project_owned_reference_navigation_which_owns_additional(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 397, 411, Justification = Upstream.GaveNoReason)]
    // Issue #34068
    public override Task Project_owned_reference_navigation_which_does_not_own_additional(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Project_owned_reference_navigation_which_does_not_own_additional(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 413, 425, Justification = Upstream.GaveNoReason)]
    public override Task No_ignored_include_warning_when_implicit_load(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.No_ignored_include_warning_when_implicit_load(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 427, 437, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Client_method_skip_loads_owned_navigations(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_method_skip_loads_owned_navigations(async));

            Assert.Equal(CosmosStrings.OffsetRequiresLimit, exception.Message);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 439, 450, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Client_method_skip_loads_owned_navigations_variation_2(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception =
                await Assert.ThrowsAsync<InvalidOperationException>(()
                    => base.Client_method_skip_loads_owned_navigations_variation_2(async));

            Assert.Equal(CosmosStrings.OffsetRequiresLimit, exception.Message);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 452, 477, Justification = Upstream.GaveNoReason)]
    public override Task Where_owned_collection_navigation_ToList_Count(bool async)
        => NoSyncTest(
            async, async a =>
            {
                // TODO: #34011
                // We override this test because the test data for this class gets saved incorrectly - the Order.Details collection gets
                // persisted as null instead of [] when there are no Details. So we change the Count we check to 1.
                await AssertQuery(
                    a,
                    ss => ss.Set<OwnedPerson>()
                        .OrderBy(p => p.Id)
                        .SelectMany(p => p.Orders)
                        .Select(p => p.Details.ToList())
                        .Where(e => e.Count() == 1),
                    assertOrder: true,
                    elementAsserter: (e, a) => AssertCollection(e, a));


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 479, 504, Justification = Upstream.GaveNoReason)]
    public override Task Where_collection_navigation_ToArray_Count(bool async)
        => NoSyncTest(
            async, async a =>
            {
                // TODO: #34011
                // We override this test because the test data for this class gets saved incorrectly - the Order.Details collection gets
                // persisted as null instead of [] when there are no Details. So we change the Count we check to 1.
                await AssertQuery(
                    a,
                    ss => ss.Set<OwnedPerson>()
                        .OrderBy(p => p.Id)
                        .SelectMany(p => p.Orders)
                        .Select(p => p.Details.AsEnumerable().ToArray())
                        .Where(e => e.Count() == 1),
                    assertOrder: true,
                    elementAsserter: (e, a) => AssertCollection(e, a));


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 506, 531, Justification = Upstream.GaveNoReason)]
    public override Task Where_collection_navigation_AsEnumerable_Count(bool async)
        => NoSyncTest(
            async, async a =>
            {
                // TODO: #34011
                // We override this test because the test data for this class gets saved incorrectly - the Order.Details collection gets
                // persisted as null instead of [] when there are no Details. So we change the Count we check to 1.
                await AssertQuery(
                    a,
                    ss => ss.Set<OwnedPerson>()
                        .OrderBy(p => p.Id)
                        .SelectMany(p => p.Orders)
                        .Select(p => p.Details.AsEnumerable())
                        .Where(e => e.Count() == 1),
                    assertOrder: true,
                    elementAsserter: (e, a) => AssertCollection(e, a));


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 533, 558, Justification = Upstream.GaveNoReason)]
    public override Task Where_collection_navigation_ToList_Count_member(bool async)
        => NoSyncTest(
            async, async a =>
            {
                // TODO: #34011
                // We override this test because the test data for this class gets saved incorrectly - the Order.Details collection gets
                // persisted as null instead of [] when there are no Details. So we change the Count we check to 1.
                await AssertQuery(
                    a,
                    ss => ss.Set<OwnedPerson>()
                        .OrderBy(p => p.Id)
                        .SelectMany(p => p.Orders)
                        .Select(p => p.Details.ToList())
                        .Where(e => e.Count == 1),
                    assertOrder: true,
                    elementAsserter: (e, a) => AssertCollection(e, a));


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 560, 585, Justification = Upstream.GaveNoReason)]
    public override Task Where_collection_navigation_ToArray_Length_member(bool async)
        => NoSyncTest(
            async, async a =>
            {
                // TODO: #34011
                // We override this test because the test data for this class gets saved incorrectly - the Order.Details collection gets persisted
                // as null instead of [].
                await AssertQuery(
                    a,
                    ss => ss.Set<OwnedPerson>()
                        .OrderBy(p => p.Id)
                        .SelectMany(p => p.Orders)
                        .Select(p => p.Details.AsEnumerable().ToArray())
                        .Where(e => e.Length == 1),
                    assertOrder: true,
                    elementAsserter: (e, a) => AssertCollection(e, a));


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 587, 589, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task GroupBy_with_multiple_aggregates_on_owned_navigation_properties(bool async)
        => AssertTranslationFailed(() => base.GroupBy_with_multiple_aggregates_on_owned_navigation_properties(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 591, 603, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_on_indexer_properties(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_query_on_indexer_properties(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 605, 617, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_on_owned_indexer_properties(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_query_on_owned_indexer_properties(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 619, 631, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_on_indexer_property_when_property_name_from_closure(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_query_on_indexer_property_when_property_name_from_closure(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 633, 645, Justification = Upstream.GaveNoReason)]
    public override Task Can_project_indexer_properties(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_project_indexer_properties(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 647, 659, Justification = Upstream.GaveNoReason)]
    public override Task Can_project_owned_indexer_properties(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_project_owned_indexer_properties(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 661, 673, Justification = Upstream.GaveNoReason)]
    public override Task Can_project_indexer_properties_converted(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_project_indexer_properties_converted(a);


            });

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 675, 697, Skip = true, Justification = "ORDER BY with expressions/functions not supported", Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/238 (ORDER BY with expressions/functions not supported)
    public override async Task Can_OrderBy_indexer_properties(bool async)
    {
        SkipOnLinuxEmulator();

        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Can_OrderBy_indexer_properties(async));

            Assert.Contains(
                "The order by query does not have a corresponding composite index that it can be served from.",
                exception.Message);


        }
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 699, 721, Skip = true, Justification = "ORDER BY with expressions/functions not supported", Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/238 (ORDER BY with expressions/functions not supported)
    public override async Task Can_OrderBy_indexer_properties_converted(bool async)
    {
        SkipOnLinuxEmulator();

        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Can_OrderBy_indexer_properties_converted(async));

            Assert.Contains(
                "The order by query does not have a corresponding composite index that it can be served from.",
                exception.Message);


        }
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 723, 745, Skip = true, Justification = "ORDER BY with expressions/functions not supported", Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/238 (ORDER BY with expressions/functions not supported)
    public override async Task Can_OrderBy_owned_indexer_properties(bool async)
    {
        SkipOnLinuxEmulator();

        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Can_OrderBy_owned_indexer_properties(async));

            Assert.Contains(
                "The order by query does not have a corresponding composite index that it can be served from.",
                exception.Message);


        }
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 747, 769, Skip = true, Justification = "ORDER BY with expressions/functions not supported", Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/238 (ORDER BY with expressions/functions not supported)
    public override async Task Can_OrderBy_owned_indexer_properties_converted(bool async)
    {
        SkipOnLinuxEmulator();

        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Can_OrderBy_owned_indexer_properties_converted(async));

            Assert.Contains(
                "The order by query does not have a corresponding composite index that it can be served from.",
                exception.Message);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 771, 773, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task Can_group_by_indexer_property(bool async)
        => AssertTranslationFailed(() => base.Can_group_by_indexer_property(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 775, 777, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task Can_group_by_converted_indexer_property(bool async)
        => AssertTranslationFailed(() => base.Can_group_by_converted_indexer_property(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 779, 781, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task Can_group_by_owned_indexer_property(bool async)
        => AssertTranslationFailed(() => base.Can_group_by_owned_indexer_property(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 783, 785, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task Can_group_by_converted_owned_indexer_property(bool async)
        => AssertTranslationFailed(() => base.Can_group_by_converted_owned_indexer_property(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 787, 789, Justification = Upstream.GaveNoReason)]
    // Uncorrelated JOINS aren't supported by Cosmos
    public override Task Can_join_on_indexer_property_on_query(bool async)
        => AssertTranslationFailed(() => base.Can_group_by_converted_owned_indexer_property(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 791, 803, Justification = Upstream.GaveNoReason)]
    public override Task Projecting_indexer_property_ignores_include(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Projecting_indexer_property_ignores_include(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 805, 817, Justification = Upstream.GaveNoReason)]
    public override Task Projecting_indexer_property_ignores_include_converted(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Projecting_indexer_property_ignores_include_converted(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 819, 822, Justification = Upstream.GaveNoReason)]
    public override Task Indexer_property_is_pushdown_into_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Indexer_property_is_pushdown_into_subquery(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 824, 842, Skip = true, Justification = "Aggregates over subqueries return null result set")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/330 (Aggregates over subqueries return null result set)
    public override Task Can_query_indexer_property_on_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                SkipOnLinuxEmulator();

                await base.Can_query_indexer_property_on_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 844, 849, Justification = Upstream.GaveNoReason)]
    public override async Task NoTracking_Include_with_cycles_throws(bool async)
    {
        await base.NoTracking_Include_with_cycles_throws(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 851, 857, Justification = Upstream.GaveNoReason)]
    // Order.Client is a non-owned navigation, cross-document join
    public override Task NoTracking_Include_with_cycles_does_not_throw_when_performing_identity_resolution(
        bool async,
        bool useAsTracking)
        => AssertTranslationFailedWithDetails(
            () => base.NoTracking_Include_with_cycles_does_not_throw_when_performing_identity_resolution(async, useAsTracking),
            CosmosStrings.CrossDocumentJoinNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 859, 864, Justification = Upstream.GaveNoReason)]
    public override async Task Trying_to_access_non_existent_indexer_property_throws_meaningful_exception(bool async)
    {
        await base.Trying_to_access_non_existent_indexer_property_throws_meaningful_exception(async);


    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 866, 888, Skip = true, Justification = "ORDER BY with expressions/functions not supported", Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/238 (ORDER BY with expressions/functions not supported)
    public override async Task Ordering_by_identifying_projection(bool async)
    {
        SkipOnLinuxEmulator();

        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Ordering_by_identifying_projection(async));

            Assert.Contains(
                "The order by query does not have a corresponding composite index that it can be served from.",
                exception.Message);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 890, 909, Justification = Upstream.GaveNoReason)]
    public override Task Query_on_collection_entry_works_for_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Query_on_collection_entry_works_for_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 911, 919, Justification = Upstream.GaveNoReason)]
    public override async Task Projecting_collection_correlated_with_keyless_entity_after_navigation_works_using_parent_identifiers(
        bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Projecting_collection_correlated_with_keyless_entity_after_navigation_works_using_parent_identifiers(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(Barton), nameof(Fink)));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 921, 928, Justification = Upstream.GaveNoReason)]
    public override async Task Left_join_on_entity_with_owned_navigations(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Left_join_on_entity_with_owned_navigations(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(OwnedPerson), nameof(Planet)));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 930, 937, Justification = Upstream.GaveNoReason)]
    public override async Task Left_join_on_entity_with_owned_navigations_complex(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Left_join_on_entity_with_owned_navigations_complex(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(OwnedPerson), nameof(Planet)));


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 939, 941, Justification = Upstream.GaveNoReason)]
    // TODO: GroupBy, #17313
    public override Task GroupBy_aggregate_on_owned_navigation_in_aggregate_selector(bool async)
        => AssertTranslationFailed(() => base.GroupBy_aggregate_on_owned_navigation_in_aggregate_selector(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 943, 955, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_indexer_using_closure(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Filter_on_indexer_using_closure(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 957, 969, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_indexer_using_function_argument(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Filter_on_indexer_using_function_argument(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 971, 973, Justification = Upstream.GaveNoReason)]
    // Non-correlated queries not supported by Cosmos
    public override Task Preserve_includes_when_applying_skip_take_after_anonymous_type_select(bool async)
        => AssertTranslationFailed(() => base.Preserve_includes_when_applying_skip_take_after_anonymous_type_select(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 975, 987, Justification = Upstream.GaveNoReason)]
    public override Task Can_project_owned_indexer_properties_converted(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_project_owned_indexer_properties_converted(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 989, 1001, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_owner_with_different_owned_types_having_same_property_name_in_hierarchy(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Can_query_owner_with_different_owned_types_having_same_property_name_in_hierarchy(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1003, 1020, Justification = Upstream.GaveNoReason)]
    public override Task Client_method_skip_take_loads_owned_navigations_variation_2(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Client_method_skip_take_loads_owned_navigations_variation_2(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1022, 1039, Justification = Upstream.GaveNoReason)]
    public override Task Client_method_skip_take_loads_owned_navigations(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Client_method_skip_take_loads_owned_navigations(a);


            });

    [StoreDefect("1.23", UpstreamRepository.EfCore, Source, 1041, 1061, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Non_nullable_property_through_optional_navigation(bool async)
    {
        // Sync always throws before getting to exception being tested.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            await NoSyncTest(
                async, async a =>
                {
                    await Assert.ThrowsAsync<NullReferenceException>(() => AssertQuery(
                        async,
                        ss => ss.Set<Barton>().Select(e => new { e.Throned!.Value })));


                });
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1063, 1092, Skip = true, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override async Task Owned_entity_without_owner_does_not_throw_for_identity_resolution(bool async, bool useAsTracking)
    {
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            using var context = CreateContext();
            var query = context.Set<OwnedPerson>().Select(e => new { e.Id, e.PersonAddress });

            query = useAsTracking
                ? query.AsTracking(QueryTrackingBehavior.NoTrackingWithIdentityResolution)
                : query.AsNoTrackingWithIdentityResolution();

            var result = await query.ToListAsync();

            Assert.Equal(4, result.Count);
            Assert.Collection(
                result.OrderBy(e => e.Id),
                element => AssertProjectedPersonAddress(1, element.Id, element.PersonAddress!),
                element => AssertProjectedPersonAddress(2, element.Id, element.PersonAddress!),
                element => AssertProjectedPersonAddress(3, element.Id, element.PersonAddress!),
                element => AssertProjectedPersonAddress(4, element.Id, element.PersonAddress!));
            Assert.Empty(context.ChangeTracker.Entries());


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1101, 1113, Justification = Upstream.GaveNoReason)]
    public override Task Simple_query_entity_with_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Simple_query_entity_with_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1115, 1127, Justification = Upstream.GaveNoReason)]
    public override Task Throw_for_owned_entities_without_owner_in_tracking_query(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Throw_for_owned_entities_without_owner_in_tracking_query(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1129, 1141, Justification = Upstream.GaveNoReason)]
    public override Task Unmapped_property_projection_loads_owned_navigations(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Unmapped_property_projection_loads_owned_navigations(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1143, 1159, Justification = Upstream.GaveNoReason)]
    public override Task Client_method_take_loads_owned_navigations(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Client_method_take_loads_owned_navigations(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1161, 1178, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Client_method_take_loads_owned_navigations_variation_2(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Client_method_take_loads_owned_navigations_variation_2(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1180, 1193, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Count_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Count_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1195, 1208, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Any_without_predicate_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Any_without_predicate_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1210, 1226, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Any_with_predicate_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Any_with_predicate_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1228, 1244, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Contains_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Contains_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1246, 1259, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task ElementAt_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.ElementAt_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1261, 1274, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task ElementAtOrDefault_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.ElementAtOrDefault_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1276, 1296, Skip = true, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData, DeviationNote = "The wire preserves CosmosException as InfoCarrierServerException; status is checked in the preserved SDK message.")]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override async Task OrderBy_ElementAt_over_owned_collection(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.OrderBy_ElementAt_over_owned_collection(async));

            Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1298, 1311, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Skip_Take_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Skip_Take_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1313, 1329, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task FirstOrDefault_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.FirstOrDefault_over_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1331, 1342, Skip = true, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override async Task Distinct_over_owned_collection(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            // TODO: Subquery pushdown, #33968
            await AssertTranslationFailed(() => base.Distinct_over_owned_collection(async));


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1344, 1363, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public override Task Union_over_owned_collection(bool async)
        => NoSyncTest(
            async, async a =>
            {
                await base.Union_over_owned_collection(a);


            });
    private static void AssertProjectedPersonAddress(int expectedId, int id, OwnedAddress personAddress)
    {
        Assert.Equal(expectedId, id);
        Assert.Equal("Land", personAddress.PlaceType);
        Assert.Equal("USA", personAddress.Country!.Name);
    }

    private static void SkipOnLinuxEmulator() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
    private static async Task<InfoCarrierServerException> AssertCosmosExceptionAsync(Func<Task> action)
    {
        var error = await CosmosExceptionAssert.ThrowsAsync(action);
        Assert.Equal("Microsoft.Azure.Cosmos.CosmosException", error.ServerExceptionTypeName);
        return error;
    }
    private static async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async) await test(true);
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal(CosmosStrings.SyncNotSupported, error.Message);
        }
    }

    public class CosmosFixture : OwnedQueryFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.HasDefaultContainer("OwnedQueryTest");

            modelBuilder.Entity<OwnedPerson>(eb =>
            {
                eb.ToContainer("OwnedPeople");
                eb.IndexerProperty<string>("Name");
                eb.HasData(
                    new
                    {
                        Id = 1,
                        id = Guid.NewGuid().ToString(),
                        Name = "Mona Cy"
                    });

                eb.OwnsOne(
                    p => p.PersonAddress, ab =>
                    {
                        ab.IndexerProperty<string>("AddressLine");
                        ab.IndexerProperty(typeof(int), "ZipCode");
                        ab.HasData(
                            new
                            {
                                OwnedPersonId = 1,
                                PlaceType = "Land",
                                AddressLine = "804 S. Lakeshore Road",
                                ZipCode = 38654
                            },
                            new
                            {
                                OwnedPersonId = 2,
                                PlaceType = "Land",
                                AddressLine = "7 Church Dr.",
                                ZipCode = 28655
                            },
                            new
                            {
                                OwnedPersonId = 3,
                                PlaceType = "Land",
                                AddressLine = "72 Hickory Rd.",
                                ZipCode = 07728
                            },
                            new
                            {
                                OwnedPersonId = 4,
                                PlaceType = "Land",
                                AddressLine = "28 Strawberry St.",
                                ZipCode = 19053
                            });

                        ab.OwnsOne(
                            a => a.Country, cb =>
                            {
                                cb.HasData(
                                    new
                                    {
                                        OwnedAddressOwnedPersonId = 1,
                                        PlanetId = 1,
                                        Name = "USA"
                                    },
                                    new
                                    {
                                        OwnedAddressOwnedPersonId = 2,
                                        PlanetId = 1,
                                        Name = "USA"
                                    },
                                    new
                                    {
                                        OwnedAddressOwnedPersonId = 3,
                                        PlanetId = 1,
                                        Name = "USA"
                                    },
                                    new
                                    {
                                        OwnedAddressOwnedPersonId = 4,
                                        PlanetId = 1,
                                        Name = "USA"
                                    });

                                cb.HasOne(cc => cc.Planet).WithMany().HasForeignKey(ee => ee.PlanetId)
                                    .OnDelete(DeleteBehavior.Restrict);
                            });
                    });

                eb.OwnsMany(
                    p => p.Orders, ob =>
                    {
                        ob.HasKey(o => o.Id);
                        ob.IndexerProperty<DateTime>("OrderDate");
                        ob.HasData(
                            new
                            {
                                Id = -10,
                                ClientId = 1,
                                OrderDate = Convert.ToDateTime("2018-07-11 10:01:41")
                            },
                            new
                            {
                                Id = -11,
                                ClientId = 1,
                                OrderDate = Convert.ToDateTime("2015-03-03 04:37:59")
                            },
                            new
                            {
                                Id = -20,
                                ClientId = 2,
                                OrderDate = Convert.ToDateTime("2015-05-25 20:35:48")
                            },
                            new
                            {
                                Id = -30,
                                ClientId = 3,
                                OrderDate = Convert.ToDateTime("2014-11-10 04:32:42")
                            },
                            new
                            {
                                Id = -40,
                                ClientId = 4,
                                OrderDate = Convert.ToDateTime("2016-04-25 19:23:56")
                            }
                        );

                        ob.OwnsMany(
                            e => e.Details, odb => odb.HasData(
                                new
                                {
                                    Id = -100,
                                    OrderId = -10,
                                    OrderClientId = 1,
                                    Detail = "Discounted Order"
                                },
                                new
                                {
                                    Id = -101,
                                    OrderId = -10,
                                    OrderClientId = 1,
                                    Detail = "Full Price Order"
                                },
                                new
                                {
                                    Id = -200,
                                    OrderId = -20,
                                    OrderClientId = 2,
                                    Detail = "Internal Order"
                                },
                                new
                                {
                                    Id = -300,
                                    OrderId = -30,
                                    OrderClientId = 3,
                                    Detail = "Bulk Order"
                                }));
                    });
            });

            modelBuilder.Entity<Branch>(eb =>
            {
                eb.HasData(
                    new
                    {
                        Id = 2,
                        id = Guid.NewGuid().ToString(),
                        Name = "Antigonus Mitul"
                    });

                eb.OwnsOne(
                    p => p.BranchAddress, ab =>
                    {
                        ab.IndexerProperty<string>("BranchName").IsRequired();
                        ab.HasData(
                            new
                            {
                                BranchId = 2,
                                PlaceType = "Land",
                                BranchName = "BranchA"
                            },
                            new
                            {
                                BranchId = 3,
                                PlaceType = "Land",
                                BranchName = "BranchB"
                            });

                        ab.OwnsOne(
                            a => a.Country, cb => cb.HasData(
                                new
                                {
                                    OwnedAddressBranchId = 2,
                                    PlanetId = 1,
                                    Name = "Canada"
                                },
                                new
                                {
                                    OwnedAddressBranchId = 3,
                                    PlanetId = 1,
                                    Name = "Canada"
                                }));
                    });
            });

            modelBuilder.Entity<LeafA>(eb =>
            {
                eb.HasData(
                    new
                    {
                        Id = 3,
                        id = Guid.NewGuid().ToString(),
                        Name = "Madalena Morana"
                    });

                eb.OwnsOne(
                    p => p.LeafAAddress, ab =>
                    {
                        ab.IndexerProperty<int>("LeafType");
                        ab.HasData(
                            new
                            {
                                LeafAId = 3,
                                PlaceType = "Land",
                                LeafType = 1
                            });

                        ab.OwnsOne(
                            a => a.Country, cb => cb.HasData(
                                new
                                {
                                    OwnedAddressLeafAId = 3,
                                    PlanetId = 1,
                                    Name = "Mexico"
                                }));
                    });
            });

            modelBuilder.Entity<LeafB>(eb =>
            {
                eb.HasData(
                    new
                    {
                        Id = 4,
                        id = Guid.NewGuid().ToString(),
                        Name = "Vanda Waldemar"
                    });

                eb.OwnsOne(
                    p => p.LeafBAddress, ab =>
                    {
                        ab.IndexerProperty<string>("LeafBType").IsRequired();
                        ab.HasData(
                            new
                            {
                                LeafBId = 4,
                                PlaceType = "Land",
                                LeafBType = "Green"
                            });

                        ab.OwnsOne(
                            a => a.Country, cb => cb.HasData(
                                new
                                {
                                    OwnedAddressLeafBId = 4,
                                    PlanetId = 1,
                                    Name = "Panama"
                                }));
                    });
            });

            modelBuilder.Entity<Planet>(pb =>
            {
                pb.ToContainer("Planets");
                pb.HasDiscriminatorInJsonId();
                pb.HasData(
                    new
                    {
                        Id = 1,
                        id = Guid.NewGuid().ToString(),
                        StarId = 1
                    });
            });

            modelBuilder.Entity<Moon>(mb =>
            {
                mb.ToContainer("Planets");
                mb.HasDiscriminatorInJsonId();
                mb.HasData(
                    new
                    {
                        Id = 1,
                        id = Guid.NewGuid().ToString(),
                        PlanetId = 1,
                        Diameter = 3474
                    });
            });

            modelBuilder.Entity<Star>(sb =>
            {
                sb.ToContainer("Planets");
                sb.HasDiscriminatorInJsonId();
                sb.HasData(
                    new
                    {
                        Id = 1,
                        id = Guid.NewGuid().ToString(),
                        Name = "Sol"
                    });

                sb.OwnsMany(
                    s => s.Composition, ob =>
                    {
                        ob.HasKey(o => o.Id);
                        ob.HasData(
                            new
                            {
                                Id = "H",
                                Name = "Hydrogen",
                                StarId = 1
                            },
                            new
                            {
                                Id = "He",
                                Name = "Helium",
                                StarId = 1
                            });
                    });
            });

            modelBuilder.Entity<Barton>(b =>
            {
                b.ToContainer("Bartons");
                b.HasDiscriminatorInJsonId();
                b.OwnsOne(
                    e => e.Throned, b => b.HasData(
                        new
                        {
                            BartonId = 1,
                            Property = "Property",
                            Value = 42
                        }));
                b.HasData(
                    new Barton { Id = 1, Simple = "Simple" },
                    new Barton { Id = 2, Simple = "Not" });
            });

            modelBuilder.Entity<Fink>(b =>
            {
                b.ToContainer("Bartons");
                b.HasDiscriminatorInJsonId();
                b.HasData(
                    new { Id = 1, BartonId = 1 });
            });

            modelBuilder
                .Entity<Balloon>()
                .ToContainer("Balloons");

            modelBuilder
                .Entity<HydrogenBalloon>()
                .OwnsOne(e => e.Gas);

            modelBuilder
                .Entity<HeliumBalloon>()
                .OwnsOne(e => e.Gas);

            modelBuilder.HasEmbeddedDiscriminatorName("Terminator");
        }

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(
                builder.ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined)));
    }
}

