// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using NorthwindCustomer = Microsoft.EntityFrameworkCore.TestModels.Northwind.Customer;
using Xunit;
using Xunit.Sdk;

#pragma warning disable EF9102 // Adopt the official experimental paging behavior tests.

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindMiscellaneousTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindMiscellaneousQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    [InfoCarrierDesign("docs/wire-protocol.md", "cancellation-before-dispatch", Deviation = DeviationKind.SqlDiffers,
        Justification = "An already-canceled transport operation stops before constructing a backend query.",
        DeviationNote = "Direct EF emits one query-attempt diagnostic before SDK cancellation; InfoCarrier emits none. Both retain the inherited OperationCanceledException and QueryCanceled assertions.")]
    public override Task ToListAsync_with_canceled_token() => base.ToListAsync_with_canceled_token();

    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindMiscellaneousQueryCosmosTest.cs";

    private static NorthwindCustomer ProcessCustomer(NorthwindCustomer c, ManualResetEventSlim e, SemaphoreSlim s)
    {
        e.Set();
        s.Wait(TimeSpan.FromMinutes(5));
        s.Release(1);
        return c;
    }

    private static async Task AssertStoreException(Func<Task> action)
    {
        InfoCarrierServerException error = await CosmosExceptionAssert.ThrowsAsync(action);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 40, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task Simple_IQueryable(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await AssertQuery(a, ss => ss.Set<NorthwindCustomer>());
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 42, 60, Justification = Upstream.GaveNoReason)]
    public override Task Shaper_command_caching_when_parameter_names_different(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Shaper_command_caching_when_parameter_names_different(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 62, 68, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Lifting_when_subquery_nested_order_by_anonymous(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Lifting_when_subquery_nested_order_by_anonymous(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 70, 76, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Lifting_when_subquery_nested_order_by_simple(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Lifting_when_subquery_nested_order_by_simple(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 78, 85, Justification = Upstream.GaveNoReason)]
    public override Task Local_dictionary(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Local_dictionary(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 87, 99, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_self(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_self(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 101, 115, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_local(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_local(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 117, 124, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_local_composite_key(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_local_composite_key(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 126, 132, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_with_entity_equality_local_on_both_sources(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_with_entity_equality_local_on_both_sources(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 134, 146, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_local_inline(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_local_inline(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 148, 155, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_local_inline_composite_key(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_local_inline_composite_key(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 157, 169, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_null(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_null(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 171, 182, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_not_null(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_not_null(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 184, 190, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Query_when_evaluatable_queryable_method_call_with_repository(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Query_when_evaluatable_queryable_method_call_with_repository(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 199, 205, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_top_level(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_top_level(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 207, 213, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_with_DefaultIfEmpty_on_both_sources(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_with_DefaultIfEmpty_on_both_sources(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 215, 221, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_top_level_followed_by_constant_Select(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_top_level_followed_by_constant_Select(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 223, 229, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_top_level_preceded_by_constant_Select(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_top_level_preceded_by_constant_Select(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 231, 237, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_top_level_positive(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_top_level_positive(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 253, 259, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_top_level_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_top_level_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 261, 267, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 269, 275, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_is_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_is_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 277, 283, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_is_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_is_not_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 285, 291, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_one_element_SingleOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_one_element_SingleOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 293, 299, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_one_element_Single(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_one_element_Single(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 301, 307, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_one_element_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_one_element_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 309, 315, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_one_element_First(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_one_element_First(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 317, 323, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_no_elements_SingleOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_no_elements_SingleOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 325, 331, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_no_elements_Single(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_no_elements_Single(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 333, 339, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_no_elements_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_no_elements_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 341, 347, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_no_elements_First(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_no_elements_First(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 349, 355, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 357, 363, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_multiple_elements_Single(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_multiple_elements_Single(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 365, 371, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_multiple_elements_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_multiple_elements_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 373, 379, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition_entity_equality_multiple_elements_First(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition_entity_equality_multiple_elements_First(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 381, 387, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 389, 395, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition2_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition2_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 397, 403, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_query_composition2_FirstOrDefault_with_anonymous(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_query_composition2_FirstOrDefault_with_anonymous(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 405, 411, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_Subquery_Single(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_Subquery_Single(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 413, 419, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_Where_Subquery_Deep_Single(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_Where_Subquery_Deep_Single(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 421, 427, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_Where_Subquery_Deep_First(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_Where_Subquery_Deep_First(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 429, 435, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_Where_Subquery_Equality(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_Where_Subquery_Equality(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 437, 443, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_anon(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_anon(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 445, 451, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_anon_nested(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_anon_nested(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 453, 459, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_SelectMany(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_SelectMany(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 461, 467, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Let_any_subquery_anonymous(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Let_any_subquery_anonymous(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 469, 484, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 469, 484, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_arithmetic(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_arithmetic(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 486, 502, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 486, 502, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_condition_comparison(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_condition_comparison(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 504, 520, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 504, 520, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_ternary_conditions(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_ternary_conditions(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 522, 528, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_any(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_any(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 530, 541, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 530, 541, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Skip(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            Assert.Equal(
                "Cosmos SQL does not allow Offset without Limit. Consider specifying a 'Take' operation on the query.",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Skip(async))).Message);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 543, 554, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 543, 554, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Skip_no_orderby(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            Assert.Equal(
                "Cosmos SQL does not allow Offset without Limit. Consider specifying a 'Take' operation on the query.",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Skip_no_orderby(async))).Message);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 556, 572, Justification = Upstream.GaveNoReason)]
    public override Task Skip_Take(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Skip_Take(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 574, 580, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_Customers_Orders_Skip_Take(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_Customers_Orders_Skip_Take(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 582, 588, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_Customers_Orders_Skip_Take_followed_by_constant_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_Customers_Orders_Skip_Take_followed_by_constant_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 590, 596, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_Customers_Orders_Projection_With_String_Concat_Skip_Take(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_Customers_Orders_Projection_With_String_Concat_Skip_Take(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 598, 604, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_Customers_Orders_Orders_Skip_Take_Same_Properties(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_Customers_Orders_Orders_Skip_Take_Same_Properties(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 606, 613, Justification = Upstream.GaveNoReason)]
    public override async Task Take_Skip(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_Skip(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 615, 622, Justification = Upstream.GaveNoReason)]
    public override async Task Take_Skip_Distinct(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_Skip_Distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 624, 631, Justification = Upstream.GaveNoReason)]
    public override async Task Take_Skip_Distinct_Caching(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_Skip_Distinct_Caching(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 633, 639, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Take_Distinct_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Take_Distinct_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 641, 647, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Take_Where_Distinct_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Take_Where_Distinct_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 649, 660, Justification = Upstream.GaveNoReason)]
    public override Task Queryable_simple(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Queryable_simple(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 662, 673, Justification = Upstream.GaveNoReason)]
    public override Task Queryable_simple_anonymous(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Queryable_simple_anonymous(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 675, 685, Justification = Upstream.GaveNoReason)]
    public override Task Queryable_nested_simple(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Queryable_nested_simple(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 687, 701, Justification = Upstream.GaveNoReason)]
    public override Task Queryable_simple_anonymous_projection_subquery(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Queryable_simple_anonymous_projection_subquery(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 703, 717, Justification = Upstream.GaveNoReason)]
    public override Task Queryable_simple_anonymous_subquery(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Queryable_simple_anonymous_subquery(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 719, 734, Justification = Upstream.GaveNoReason)]
    public override Task Take_simple(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Take_simple(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 736, 750, Justification = Upstream.GaveNoReason)]
    public override Task Take_simple_parameterized(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Take_simple_parameterized(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 752, 766, Justification = Upstream.GaveNoReason)]
    public override Task Take_simple_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Take_simple_projection(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 768, 782, Justification = Upstream.GaveNoReason)]
    public override Task Take_subquery_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Take_subquery_projection(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 784, 790, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_Take_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_Take_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 792, 799, Justification = Upstream.GaveNoReason)]
    public override async Task Take_OrderBy_Count(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_OrderBy_Count(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 801, 813, Justification = Upstream.GaveNoReason)]
    public override Task Any_simple(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Any_simple(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 815, 828, Justification = Upstream.GaveNoReason)]
    public override Task Any_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Any_predicate(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 830, 836, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested_negated(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested_negated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 838, 844, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested_negated2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested_negated2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 846, 852, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested_negated3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested_negated3(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 854, 860, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 862, 868, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 870, 876, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_nested3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_nested3(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 878, 884, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_with_multiple_conditions_still_uses_exists(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_with_multiple_conditions_still_uses_exists(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 886, 892, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Any_on_distinct(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Any_on_distinct(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 894, 900, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Contains_on_distinct(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_on_distinct(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 902, 908, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task All_on_distinct(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.All_on_distinct(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 910, 916, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task All_top_level(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.All_top_level(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 918, 924, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task All_top_level_column(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.All_top_level_column(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 926, 932, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task All_top_level_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.All_top_level_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 934, 940, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task All_top_level_subquery_ef_property(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.All_top_level_subquery_ef_property(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 949, 955, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_or(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_or(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 957, 963, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_or2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_or2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 965, 971, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_or3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_or3(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 973, 979, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_or4(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_or4(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 981, 987, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_or_with_parameter(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_or_with_parameter(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 996, 1002, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_simple_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_simple_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1004, 1010, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_simple1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_simple1(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1012, 1018, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_simple2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_simple2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1020, 1026, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_entity_deep(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_entity_deep(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1028, 1034, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_projection1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_projection1(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1036, 1042, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_projection2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_projection2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1044, 1050, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_customer_orders(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_customer_orders(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1052, 1058, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1060, 1066, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_LongCount(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_LongCount(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1068, 1074, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_OrderBy_ThenBy_Any(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_OrderBy_ThenBy_Any(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1076, 1082, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_Where_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_Where_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1084, 1090, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Join_Any(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Join_Any(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1092, 1098, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Join_Exists(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Join_Exists(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1100, 1106, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Join_Exists_Inequality(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Join_Exists_Inequality(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1108, 1114, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Join_Exists_Constant(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Join_Exists_Constant(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1116, 1122, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Join_Not_Exists(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Join_Not_Exists(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1124, 1131, Justification = Upstream.GaveNoReason)]
    public override async Task Join_OrderBy_Count(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Join_OrderBy_Count(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1133, 1140, Justification = Upstream.GaveNoReason)]
    public override async Task Multiple_joins_Where_Order_Any(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Multiple_joins_Where_Order_Any(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1142, 1149, Justification = Upstream.GaveNoReason)]
    public override async Task Where_join_select(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Where_join_select(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1151, 1158, Justification = Upstream.GaveNoReason)]
    public override async Task Where_orderby_join_select(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Where_orderby_join_select(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1160, 1167, Justification = Upstream.GaveNoReason)]
    public override async Task Where_join_orderby_join_select(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Where_join_orderby_join_select(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1169, 1176, Justification = Upstream.GaveNoReason)]
    public override async Task Where_select_many(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Where_select_many(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1178, 1185, Justification = Upstream.GaveNoReason)]
    public override async Task Where_orderby_select_many(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Where_orderby_select_many(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1187, 1194, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_cartesian_product_with_ordering(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_cartesian_product_with_ordering(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1196, 1203, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_Joined_DefaultIfEmpty(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_Joined_DefaultIfEmpty(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1205, 1212, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_Joined_DefaultIfEmpty2(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_Joined_DefaultIfEmpty2(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1214, 1221, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_Joined_DefaultIfEmpty3(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_Joined_DefaultIfEmpty3(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1223, 1230, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_Joined(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_Joined(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1232, 1239, Justification = Upstream.GaveNoReason)]
    public override async Task SelectMany_Joined_Take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.SelectMany_Joined_Take(async),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1241, 1248, Justification = Upstream.GaveNoReason)]
    public override async Task Take_with_single(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_with_single(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1250, 1256, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Take_with_single_select_many(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Take_with_single_select_many(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1258, 1266, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_Skip(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Distinct_Skip(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1268, 1276, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_Skip_Take(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Distinct_Skip_Take(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1278, 1281, Justification = Upstream.GaveNoReason)]
    public override Task Skip_Distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Skip_Distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1283, 1286, Justification = Upstream.GaveNoReason)]
    public override Task Skip_Take_Distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Skip_Take_Distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1288, 1294, Justification = "TODO: Subquery pushdown, #33968")]
    public override async Task Skip_Take_Any(bool async)
    {
        // TODO: Subquery pushdown, #33968
        await AssertTranslationFailed(() => base.Skip_Take_Any(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1296, 1302, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Skip_Take_All(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Skip_Take_All(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1304, 1310, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Take_All(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Take_All(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1312, 1315, Justification = Upstream.GaveNoReason)]
    public override Task Skip_Take_Any_with_predicate(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Skip_Take_Any_with_predicate(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1317, 1320, Justification = Upstream.GaveNoReason)]
    public override Task Take_Any_with_predicate(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Take_Any_with_predicate(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1322, 1333, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1335, 1350, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1335, 1350, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_true(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_true(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1352, 1366, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1352, 1366, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_integer(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_integer(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1368, 1385, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1368, 1385, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_parameter(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_parameter(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1387, 1398, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_anon(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_anon(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1400, 1411, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_anon2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_anon2(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1420, 1427, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.UpstreamCallsAnotherTest, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override Task OrderBy_multiple_queries(bool async) => base.OrderBy_multiple_queries(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1429, 1432, Justification = Upstream.GaveNoReason)]
    public override Task Take_Distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Take_Distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1434, 1437, Justification = Upstream.GaveNoReason)]
    public override Task Distinct_Take(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Distinct_Take(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1439, 1445, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Distinct_Take_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Distinct_Take_Count(async));
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 1447, 1464, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderBy_shadow(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 1466, 1485, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderBy_multiple(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1487, 1499, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_ThenBy_Any(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_ThenBy_Any(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1501, 1507, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_correlated_subquery1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_correlated_subquery1(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1509, 1515, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_correlated_subquery2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_correlated_subquery2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1517, 1523, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_recursive_trivial(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_recursive_trivial(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1532, 1550, Skip = true, Justification = "Always does sync evaluation.")]
    public override Task Where_subquery_expression(bool async) => throw SkipException.ForSkip("Always does sync evaluation.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1552, 1570, Skip = true, Justification = "Always does sync evaluation.")]
    public override Task Where_subquery_expression_same_parametername(bool async) => throw SkipException.ForSkip("Always does sync evaluation.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1572, 1584, Justification = Upstream.GaveNoReason)]
    public override Task Select_DTO_distinct_translated_to_server(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_DTO_distinct_translated_to_server(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1586, 1598, Justification = Upstream.GaveNoReason)]
    public override Task Select_DTO_constructor_distinct_translated_to_server(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_DTO_constructor_distinct_translated_to_server(a);
            });

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 1600, 1621, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Select_DTO_with_member_init_distinct_translated_to_server(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1623, 1629, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_count_using_DTO(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_count_using_DTO(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1631, 1637, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_DTO_with_member_init_distinct_in_subquery_translated_to_server(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_DTO_with_member_init_distinct_in_subquery_translated_to_server(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1639, 1646, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_DTO_with_member_init_distinct_in_subquery_used_in_projection_translated_to_server(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Select_DTO_with_member_init_distinct_in_subquery_used_in_projection_translated_to_server(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1648, 1654, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_correlated_subquery_filtered(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_correlated_subquery_filtered(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1656, 1662, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_correlated_subquery_ordered(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_correlated_subquery_ordered(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1664, 1670, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_nested_collection_in_anonymous_type(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_in_anonymous_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1672, 1678, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_subquery_recursive_trivial(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_subquery_recursive_trivial(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1680, 1686, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_on_bool(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_on_bool(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1688, 1694, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_on_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_on_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1696, 1702, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_many_cross_join_same_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_many_cross_join_same_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1704, 1719, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1704, 1719, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_null_coalesce_operator(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_null_coalesce_operator(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1721, 1740, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1721, 1740, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task Select_null_coalesce_operator(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.Select_null_coalesce_operator(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1742, 1756, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1742, 1756, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_conditional_operator(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_conditional_operator(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1758, 1770, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_conditional_operator_where_condition_false(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_conditional_operator_where_condition_false(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1772, 1787, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1772, 1787, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_comparison_operator(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.OrderBy_comparison_operator(async));
        }
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 1789, 1809, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Projection_null_coalesce_operator(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1811, 1822, Justification = Upstream.GaveNoReason)]
    public override Task Filter_coalesce_operator(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Filter_coalesce_operator(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1824, 1831, Justification = Upstream.GaveNoReason)]
    public override async Task Take_skip_null_coalesce_operator(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Take_skip_null_coalesce_operator(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1833, 1856, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1833, 1856, Case = "async: True", Justification = "Subquery pushdown. Issue #16156.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task Select_take_null_coalesce_operator(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Subquery pushdown. Issue #16156.
            await AssertStoreException(async () => await base.Select_take_null_coalesce_operator(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1858, 1865, Justification = Upstream.GaveNoReason)]
    public override async Task Select_take_skip_null_coalesce_operator(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Select_take_skip_null_coalesce_operator(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1867, 1874, Justification = Upstream.GaveNoReason)]
    public override async Task Select_take_skip_null_coalesce_operator2(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Select_take_skip_null_coalesce_operator2(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1876, 1883, Justification = Upstream.GaveNoReason)]
    public override async Task Select_take_skip_null_coalesce_operator3(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Select_take_skip_null_coalesce_operator3(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1885, 1899, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1885, 1899, Case = "async: True", Justification = "Unsupported ORDER BY clause.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task Selected_column_can_coalesce(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Unsupported ORDER BY clause.
            await AssertStoreException(() => base.Selected_column_can_coalesce(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1901, 1925, Justification = Upstream.GaveNoReason)]
    public override Task Environment_newline_is_funcletized(bool async) => Fixture.NoSyncTest(async, base.Environment_newline_is_funcletized);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1927, 1933, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task String_concat_with_navigation1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.String_concat_with_navigation1(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1935, 1941, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task String_concat_with_navigation2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.String_concat_with_navigation2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1943, 1949, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Handle_materialization_properly_when_more_than_two_query_sources_are_involved(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Handle_materialization_properly_when_more_than_two_query_sources_are_involved(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1951, 1973, Justification = "Optimize query SQL. Issue #13159.")]
    public override Task Parameter_extraction_short_circuits_1(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Parameter_extraction_short_circuits_1(a);

                // Optimize query SQL. Issue #13159.
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1975, 1997, Justification = "Optimize query SQL. Issue #13159.")]
    public override Task Parameter_extraction_short_circuits_2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Parameter_extraction_short_circuits_2(a);

                // Optimize query SQL. Issue #13159.
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1999, 2021, Justification = "Optimize query SQL. Issue #13159.")]
    public override Task Parameter_extraction_short_circuits_3(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Parameter_extraction_short_circuits_3(a);

                // Optimize query SQL. Issue #13159.
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2023, 2033, Justification = Upstream.GaveNoReason)]
    public override Task Captured_variable_from_switch_case_pattern_matching(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Captured_variable_from_switch_case_pattern_matching(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2035, 2041, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Subquery_member_pushdown_does_not_change_original_subquery_model(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Subquery_member_pushdown_does_not_change_original_subquery_model(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2043, 2049, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Query_expression_with_to_string_and_contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Query_expression_with_to_string_and_contains(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2051, 2063, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_long_to_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_long_to_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2065, 2077, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_int_to_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_int_to_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2079, 2097, Justification = Upstream.GaveNoReason)]
    public override Task ToString_with_formatter_is_evaluated_on_the_client(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.ToString_with_formatter_is_evaluated_on_the_client(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2099, 2111, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_other_to_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_other_to_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2113, 2125, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_date_add_year(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_date_add_year(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2127, 2139, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_datetime_add_month(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_datetime_add_month(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2141, 2153, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_datetime_add_hour(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_datetime_add_hour(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2155, 2167, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_datetime_add_minute(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_datetime_add_minute(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2169, 2181, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_datetime_add_second(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_datetime_add_second(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2183, 2195, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_date_add_milliseconds_above_the_range(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_date_add_milliseconds_above_the_range(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2197, 2213, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_date_add_milliseconds_below_the_range(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await AssertQuery(
                    a,
                    ss => ss.Set<Order>().Where(o => o.OrderDate != null)
                        .Select(o => new Order { OrderDate = o.OrderDate!.Value.AddMilliseconds(-1000000000000) }),
                    elementSorter: e => e.OrderDate);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2215, 2229, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_date_add_milliseconds_large_number_divided(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_date_add_milliseconds_large_number_divided(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2231, 2244, Justification = Upstream.GaveNoReason)]
    public override Task Add_minutes_on_constant_value(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Add_minutes_on_constant_value(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2246, 2260, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_references_are_updated_correctly_with_subquery(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_references_are_updated_correctly_with_subquery(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2262, 2268, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_without_group_join(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_without_group_join(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2270, 2276, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_in_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_in_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2278, 2284, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_in_subquery_not_correlated(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_in_subquery_not_correlated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2286, 2292, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_in_subquery_nested(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_in_subquery_nested(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2294, 2301, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DefaultIfEmpty_in_subquery_nested_filter_order_comparison(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_in_subquery_nested_filter_order_comparison(async));
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 2303, 2325, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderBy_skip_take(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2327, 2334, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_skip_skip_take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_skip_take(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2336, 2343, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_skip_take_take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_take_take(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2345, 2352, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_skip_take_take_take_take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_take_take_take_take(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2354, 2362, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_skip_take_skip_take_skip(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_take_skip_take_skip(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2364, 2367, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_skip_take_distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_take_distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2369, 2372, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_coalesce_take_distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.OrderBy_coalesce_take_distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2374, 2377, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_coalesce_skip_take_distinct(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.OrderBy_coalesce_skip_take_distinct(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2379, 2386, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_coalesce_skip_take_distinct_take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_coalesce_skip_take_distinct_take(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2388, 2395, Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_skip_take_distinct_orderby_take(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.OrderBy_skip_take_distinct_orderby_take(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2397, 2403, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task No_orderby_added_for_fully_translated_manually_constructed_LOJ(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.No_orderby_added_for_fully_translated_manually_constructed_LOJ(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2435, 2441, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Contains_with_DateTime_Date(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_DateTime_Date(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2443, 2449, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Contains_with_subquery_involving_join_binds_to_correct_table(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_subquery_involving_join_binds_to_correct_table(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2451, 2457, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Complex_query_with_repeated_query_model_compiles_correctly(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Complex_query_with_repeated_query_model_compiles_correctly(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2459, 2465, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Complex_query_with_repeated_nested_query_model_compiles_correctly(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Complex_query_with_repeated_nested_query_model_compiles_correctly(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2467, 2478, Justification = Upstream.GaveNoReason)]
    public override Task Anonymous_member_distinct_where(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Anonymous_member_distinct_where(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2480, 2488, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Anonymous_member_distinct_orderby(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Anonymous_member_distinct_orderby(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2490, 2496, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_member_distinct_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_member_distinct_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2498, 2509, Justification = Upstream.GaveNoReason)]
    public override Task Anonymous_complex_distinct_where(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Anonymous_complex_distinct_where(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2511, 2519, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Anonymous_complex_distinct_orderby(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Anonymous_complex_distinct_orderby(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2521, 2527, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_complex_distinct_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_complex_distinct_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2529, 2543, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2529, 2543, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task Anonymous_complex_orderby(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            await AssertStoreException(async () => await base.Anonymous_complex_orderby(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2545, 2551, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_subquery_orderby(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_subquery_orderby(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2553, 2564, Justification = Upstream.GaveNoReason)]
    public override Task DTO_member_distinct_where(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.DTO_member_distinct_where(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2566, 2574, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task DTO_member_distinct_orderby(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.DTO_member_distinct_orderby(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2576, 2582, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DTO_member_distinct_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DTO_member_distinct_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2584, 2595, Justification = Upstream.GaveNoReason)]
    public override Task DTO_complex_distinct_where(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.DTO_complex_distinct_where(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2597, 2605, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task DTO_complex_distinct_orderby(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.DTO_complex_distinct_orderby(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2607, 2613, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DTO_complex_distinct_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DTO_complex_distinct_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2615, 2629, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2615, 2629, Case = "async: True", Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task DTO_complex_orderby(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertStoreException(async () => await base.DTO_complex_orderby(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2631, 2637, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DTO_subquery_orderby(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DTO_subquery_orderby(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2639, 2649, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Include_with_orderby_skip_preserves_ordering(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        Assert.Equal(
            "Including navigation 'Navigation: Customer.Orders (List<Order>) Collection ToDependent Order Inverse: Customer PropertyAccessMode.Field' is not supported as the navigation is not embedded in same resource.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Include_with_orderby_skip_preserves_ordering(async)))
            .Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2651, 2658, Justification = Upstream.GaveNoReason)]
    public override Task Int16_parameter_can_be_used_for_int_column(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Int16_parameter_can_be_used_for_int_column(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2660, 2666, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Subquery_is_null_translated_correctly(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Subquery_is_null_translated_correctly(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2668, 2674, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Subquery_is_not_null_translated_correctly(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Subquery_is_not_null_translated_correctly(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2676, 2682, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_average(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_average(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2684, 2690, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2692, 2698, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_orderBy_take_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_orderBy_take_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2700, 2706, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_long_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_long_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2708, 2714, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_orderBy_take_long_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_orderBy_take_long_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2716, 2722, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_max(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_max(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2724, 2730, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_min(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_min(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2732, 2738, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_take_sum(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_take_sum(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2740, 2746, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_average(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_average(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2748, 2754, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2756, 2762, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_orderBy_skip_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_orderBy_skip_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2764, 2770, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_long_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_long_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2772, 2778, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_orderBy_skip_long_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_orderBy_skip_long_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2780, 2786, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_max(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_max(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2788, 2794, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_min(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_min(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2796, 2802, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_skip_sum(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_skip_sum(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2804, 2810, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_average(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_average(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2812, 2818, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2820, 2826, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_long_count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_long_count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2828, 2834, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_max(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_max(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2836, 2842, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_min(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_min(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2844, 2850, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_sum(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_sum(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2852, 2865, Justification = Upstream.GaveNoReason)]
    public override Task Comparing_to_fixed_string_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Comparing_to_fixed_string_parameter(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2867, 2873, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_entities_using_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_entities_using_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2875, 2881, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_different_entity_types_using_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_different_entity_types_using_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2883, 2896, Justification = Upstream.GaveNoReason)]
    public override Task Comparing_entity_to_null_using_Equals(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Comparing_entity_to_null_using_Equals(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2898, 2904, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_navigations_using_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_navigations_using_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2906, 2912, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_navigations_using_static_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_navigations_using_static_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2914, 2920, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_non_matching_entities_using_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_non_matching_entities_using_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2922, 2928, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_non_matching_collection_navigations_using_Equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_non_matching_collection_navigations_using_Equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2930, 2941, Justification = Upstream.GaveNoReason)]
    public override Task Comparing_collection_navigation_to_null(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Comparing_collection_navigation_to_null(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2943, 2950, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Comparing_collection_navigation_to_null_complex(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Comparing_collection_navigation_to_null_complex(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2952, 2963, Justification = Upstream.GaveNoReason)]
    public override Task Compare_collection_navigation_with_itself(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Compare_collection_navigation_with_itself(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2965, 2971, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_two_collection_navigations_with_different_query_sources(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Compare_two_collection_navigations_with_different_query_sources(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2973, 2979, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_two_collection_navigations_using_equals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Compare_two_collection_navigations_using_equals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2981, 2988, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_two_collection_navigations_with_different_property_chains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Compare_two_collection_navigations_with_different_property_chains(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2990, 3002, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_ThenBy_same_column_different_direction(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_ThenBy_same_column_different_direction(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3004, 3016, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_OrderBy_same_column_different_direction(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_OrderBy_same_column_different_direction(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3018, 3025, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Complex_nested_query_doesnt_try_binding_to_grandparent_when_parent_returns_complex_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Complex_nested_query_doesnt_try_binding_to_grandparent_when_parent_returns_complex_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3027, 3034, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Complex_nested_query_properly_binds_to_grandparent_when_parent_returns_scalar_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Complex_nested_query_properly_binds_to_grandparent_when_parent_returns_scalar_result(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3036, 3051, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_Dto_projection_skip_take(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_Dto_projection_skip_take(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3053, 3059, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Join_take_count_works(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Join_take_count_works(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3061, 3076, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 3061, 3076, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_empty_list_contains(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            await AssertStoreException(async () => await base.OrderBy_empty_list_contains(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3078, 3094, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 3078, 3094, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    public override async Task OrderBy_empty_list_does_not_contains(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            await AssertStoreException(async () => await base.OrderBy_empty_list_does_not_contains(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3096, 3102, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Manual_expression_tree_typed_null_equality(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Manual_expression_tree_typed_null_equality(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3104, 3110, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Let_subquery_with_multiple_occurrences(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Let_subquery_with_multiple_occurrences(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3112, 3118, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Let_entity_equality_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Let_entity_equality_to_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3120, 3126, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Let_entity_equality_to_other_entity(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Let_entity_equality_to_other_entity(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3135, 3140, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_navigation_equal_to_null_for_subquery(bool async)
    {
        await AssertTranslationFailed(() => base.Collection_navigation_equal_to_null_for_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3142, 3148, Justification = "Left join translation. Issue #17314.")]
    public override async Task Dependent_to_principal_navigation_equal_to_null_for_subquery(bool async)
    {
        // Left join translation. Issue #17314.
        await AssertTranslationFailed(() => base.Dependent_to_principal_navigation_equal_to_null_for_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3157, 3163, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_through_nested_anonymous_type_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_through_nested_anonymous_type_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3165, 3171, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_through_DTO_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_through_DTO_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3173, 3179, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_through_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_through_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3181, 3199, Justification = Upstream.GaveNoReason)]
    public override Task Can_convert_manually_build_expression_with_default(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_convert_manually_build_expression_with_default(a);
            });

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 3201, 3220, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task Entity_equality_orderby_descending_composite_key(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3222, 3228, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_orderby_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_orderby_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3230, 3236, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_orderby_descending_subquery_composite_key(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_orderby_descending_subquery_composite_key(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3238, 3251, Justification = Upstream.GaveNoReason)]
    public override Task Coalesce_Correct_Multiple_Same_TypeMapping(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Coalesce_Correct_Multiple_Same_TypeMapping(async);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3253, 3260, Justification = Upstream.GaveNoReason)]
    public override Task Coalesce_Correct_TypeMapping_Double(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Coalesce_Correct_TypeMapping_Double(async);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3262, 3274, Justification = Upstream.GaveNoReason)]
    public override Task Coalesce_Correct_TypeMapping_String(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Coalesce_Correct_TypeMapping_String(async);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3276, 3282, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Null_Coalesce_Short_Circuit(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Null_Coalesce_Short_Circuit(async));
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 3284, 3302, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderByDescending_ThenBy(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 3304, 3321, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderByDescending_ThenByDescending(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3323, 3330, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_Join(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_Join(async));
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 3332, 3350, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderBy_ThenBy(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 3352, 3371, Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task OrderBy_ThenBy_predicate(bool async) => throw SkipException.ForSkip("ORDER BY with expressions/functions not supported");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3373, 3379, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_simple(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_simple(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3381, 3387, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_nested_simple(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_nested_simple(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3389, 3395, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_primitive(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_primitive(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3397, 3406, Justification = "Cosmos client evaluation. Issue #17246.", Deviation = DeviationKind.Other, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override async Task SelectMany_primitive_select_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        Assert.Equal(
            CoreStrings.ExpressionParameterizationExceptionSensitive(
                "value(Microsoft.EntityFrameworkCore.Query.NorthwindMiscellaneousQueryTestBase`1+<>c__DisplayClass177_0[InfoCarrier.Core.CosmosTests.Query.NorthwindFixture`1[Microsoft.EntityFrameworkCore.TestUtilities.NoopModelCustomizer]]).ss.Set().Any()"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.SelectMany_primitive_select_subquery(async))).Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3408, 3414, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_DTO_constructor_distinct_with_navigation_translated_to_server(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_DTO_constructor_distinct_with_navigation_translated_to_server(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3416, 3422, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_DTO_constructor_distinct_with_collection_projection_translated_to_server(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_DTO_constructor_distinct_with_collection_projection_translated_to_server(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3424, 3432, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task
        Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base
            .Select_DTO_constructor_distinct_with_collection_projection_translated_to_server_with_binding_after_client_eval(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3434, 3445, Justification = Upstream.GaveNoReason)]
    public override Task Select_Property_when_shadow_unconstrained_generic_method(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_Property_when_shadow_unconstrained_generic_method(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3447, 3458, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 3447, 3458, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Skip_orderby_const(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            Assert.Equal(
                "Cosmos SQL does not allow Offset without Limit. Consider specifying a 'Take' operation on the query.",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Skip_orderby_const(async))).Message);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3460, 3473, Justification = Upstream.GaveNoReason)]
    public override Task Where_Property_when_shadow_unconstrained_generic_method(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_Property_when_shadow_unconstrained_generic_method(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3475, 3481, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Inner_parameter_in_nested_lambdas_gets_preserved(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Inner_parameter_in_nested_lambdas_gets_preserved(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3483, 3489, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Navigation_inside_interpolated_string_is_expanded(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Navigation_inside_interpolated_string_is_expanded(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3491, 3497, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_DTO_with_member_init_distinct_in_subquery_translated_to_server_2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_DTO_with_member_init_distinct_in_subquery_translated_to_server_2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3513, 3519, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Subquery_member_pushdown_does_not_change_original_subquery_model2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Subquery_member_pushdown_does_not_change_original_subquery_model2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3529, 3535, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task OrderBy_object_type_server_evals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.OrderBy_object_type_server_evals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3537, 3543, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task AsQueryable_in_query_server_evals(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.AsQueryable_in_query_server_evals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3545, 3551, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_subquery_simple(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_subquery_simple(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3561, 3567, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_Select_value_type_and_DefaultIfEmpty_in_selector(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_Select_value_type_and_DefaultIfEmpty_in_selector(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3569, 3575, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_subquery_hard(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_subquery_hard(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3577, 3583, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Subquery_DefaultIfEmpty_Any(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Subquery_DefaultIfEmpty_Any(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3585, 3591, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_skip_collection_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_skip_collection_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3593, 3599, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_take_collection_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_take_collection_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3601, 3607, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_skip_take_collection_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_skip_take_collection_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3609, 3615, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_skip_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_skip_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3617, 3623, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_take_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_take_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3625, 3631, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_skip_take_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_skip_take_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3633, 3642, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_skip(bool async)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_projection_skip(async));

        Assert.Equal(
            "Including navigation 'Navigation: Order.OrderDetails (ICollection<OrderDetail>) Collection ToDependent OrderDetail Inverse: Order' is not supported as the navigation is not embedded in same resource.", ex.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3644, 3653, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_take(bool async)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_projection_take(async));

        Assert.Equal(
            "Including navigation 'Navigation: Order.OrderDetails (ICollection<OrderDetail>) Collection ToDependent OrderDetail Inverse: Order' is not supported as the navigation is not embedded in same resource.", ex.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3655, 3664, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_projection_skip_take(bool async)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_projection_skip_take(async));

        Assert.Equal(
            "Including navigation 'Navigation: Order.OrderDetails (ICollection<OrderDetail>) Collection ToDependent OrderDetail Inverse: Order' is not supported as the navigation is not embedded in same resource.", ex.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3666, 3672, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_projection_skip_empty_collection_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_projection_skip_empty_collection_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3674, 3680, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_projection_take_empty_collection_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_projection_take_empty_collection_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3682, 3688, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_projection_skip_take_empty_collection_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_projection_skip_take_empty_collection_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3690, 3713, Justification = Upstream.GaveNoReason)]
    public override Task Checked_context_with_arithmetic_does_not_fail(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                checked
                {
                    await AssertQuery(
                        a,
                        ss => ss.Set<OrderDetail>()
                            .Where(w => w.Quantity + 1 == 5 && w.Quantity - 1 == 3 && w.Quantity * 1 == w.Quantity)
                            .OrderBy(o => o.OrderID)
                            .Select(o => o),
                        elementSorter: e => (e.OrderID, e.ProductID),
                        elementAsserter: (e, a2) => AssertEqual(e, a2));
                }
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3715, 3727, Justification = Upstream.GaveNoReason)]
    public override Task Checked_context_with_case_to_same_nullable_type_does_not_fail(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Checked_context_with_case_to_same_nullable_type_does_not_fail(async);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3729, 3736, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_with_null_coalesce_client_side(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_with_null_coalesce_client_side(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3738, 3749, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Entity membership reconstruction filters null entity candidates before comparing their keys.",
        DeviationNote = "The direct id IN (null, ALFKI) predicate becomes id = ALFKI; persisted entity identifiers cannot be null and the inherited result assertions remain unchanged.")]
    public override Task Entity_equality_contains_with_list_of_null(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_contains_with_list_of_null(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3751, 3757, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Perform_identity_resolution_reuses_same_instances(bool async, bool useAsTracking)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Perform_identity_resolution_reuses_same_instances(async, useAsTracking));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3759, 3765, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Perform_identity_resolution_reuses_same_instances_across_joins(bool async, bool useAsTracking)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Perform_identity_resolution_reuses_same_instances_across_joins(async, useAsTracking));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3781, 3787, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Single_non_scalar_projection_after_skip_uses_join(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Single_non_scalar_projection_after_skip_uses_join(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3789, 3795, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_distinct_Select_with_client_bindings(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_distinct_Select_with_client_bindings(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3797, 3805, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task Pending_selector_in_cardinality_reducing_method_is_applied_before_expanding_collection_navigation_member(
        bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(()
            => base.Pending_selector_in_cardinality_reducing_method_is_applied_before_expanding_collection_navigation_member(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3807, 3812, Justification = Upstream.GaveNoReason)]
    public override async Task Max_on_empty_sequence_throws(bool async)
    {
        await AssertTranslationFailed(() => base.Max_on_empty_sequence_throws(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3814, 3822, Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_followed_by_ordering_on_condition(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Distinct_followed_by_ordering_on_condition(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3824, 3830, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task DefaultIfEmpty_Sum_over_collection_navigation(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_Sum_over_collection_navigation(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3832, 3838, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task Entity_equality_on_subquery_with_null_check(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_on_subquery_with_null_check(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3840, 3846, Justification = "DefaultIfEmpty. Issue #17246.")]
    public override async Task DefaultIfEmpty_over_empty_collection_followed_by_projecting_constant(bool async)
    {
        // DefaultIfEmpty. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_over_empty_collection_followed_by_projecting_constant(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3848, 3854, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task FirstOrDefault_with_predicate_nested(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.FirstOrDefault_with_predicate_nested(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3856, 3862, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task First_on_collection_in_projection(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.First_on_collection_in_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3864, 3870, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task Skip_0_Take_0_works_when_constant(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Skip_0_Take_0_works_when_constant(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3872, 3878, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task Skip_1_Take_0_works_when_constant(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Skip_1_Take_0_works_when_constant(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3880, 3886, Justification = "Non embedded collection subquery. Issue #17246.")]
    public override async Task Take_0_works_when_constant(bool async)
    {
        // Non embedded collection subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Take_0_works_when_constant(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3888, 3895, Justification = Upstream.GaveNoReason)]
    public override async Task Using_static_string_Equals_with_StringComparison_throws_informative_error(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Using_static_string_Equals_with_StringComparison_throws_informative_error(async),
            CoreStrings.QueryUnableToTranslateStringEqualsWithStringComparison);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3897, 3904, Justification = Upstream.GaveNoReason)]
    public override async Task Using_string_Equals_with_StringComparison_throws_informative_error(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Using_string_Equals_with_StringComparison_throws_informative_error(async),
            CoreStrings.QueryUnableToTranslateStringEqualsWithStringComparison);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3906, 3912, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Select_nested_collection_with_distinct(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_with_distinct(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3914, 3920, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Correlated_collection_with_distinct_without_default_identifiers_projecting_columns(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Correlated_collection_with_distinct_without_default_identifiers_projecting_columns(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3922, 3930, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Correlated_collection_with_distinct_without_default_identifiers_projecting_columns_with_navigation(
        bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(()
            => base.Correlated_collection_with_distinct_without_default_identifiers_projecting_columns_with_navigation(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3932, 3938, Justification = "Cross collection join. Issue #17246.")]
    public override async Task Collection_projection_after_DefaultIfEmpty(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_projection_after_DefaultIfEmpty(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3940, 3951, Justification = Upstream.GaveNoReason)]
    public override Task AsEnumerable_over_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.AsEnumerable_over_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3953, 3965, Justification = Upstream.GaveNoReason)]
    public override Task Select_Property_when_non_shadow(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_Property_when_non_shadow(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3967, 3977, Justification = Upstream.GaveNoReason)]
    public override Task Cast_results_to_object(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Cast_results_to_object(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3979, 3990, Justification = Upstream.GaveNoReason)]
    public override Task Null_Coalesce_Short_Circuit_with_server_correlated_leftover(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Null_Coalesce_Short_Circuit_with_server_correlated_leftover(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 3992, 4004, Justification = Upstream.GaveNoReason)]
    public override Task Concat_int_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Concat_int_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4006, 4018, Justification = Upstream.GaveNoReason)]
    public override Task Select_expression_datetime_add_ticks(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_expression_datetime_add_ticks(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4020, 4027, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 4020, 4027, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the fixture-created backend database; omit client database creation from upstream setup.")]
    public override async Task Throws_on_concurrent_query_first(bool async)
    {
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        using var context = CreateContext();
        // The fixture creates the backend database; the client has no database to create.

        using var synchronizationEvent = new ManualResetEventSlim(false);
        using var blockingSemaphore = new SemaphoreSlim(0);
        var blockingTask = Task.Run(async () =>
        {
            try
            {
                await context.Customers.Select(c => ProcessCustomer(c, synchronizationEvent, blockingSemaphore)).ToListAsync();
            }
            finally
            {
                synchronizationEvent.Set();
            }
        });

        var throwingTask = Task.Run(async () =>
        {
            synchronizationEvent.Wait(TimeSpan.FromMinutes(5));
            Assert.Equal(
                CoreStrings.ConcurrentMethodInvocation,
                (async
                    ? await Assert.ThrowsAsync<InvalidOperationException>(() => context.Customers.FirstAsync())
                    : Assert.Throws<InvalidOperationException>(() => context.Customers.First())).Message);
        });

        await throwingTask;

        blockingSemaphore.Release(1);

        await blockingTask;
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4029, 4040, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_through_include(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_through_include(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4042, 4054, Justification = Upstream.GaveNoReason)]
    public override Task Concat_constant_string_int(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Concat_constant_string_int(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4056, 4068, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_scalar_primitive(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_scalar_primitive(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4070, 4077, Justification = Upstream.GaveNoReason)]
    public override Task Where_Property_when_non_shadow(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_Property_when_non_shadow(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4079, 4090, Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_Select(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderBy_Select(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4092, 4104, Justification = Upstream.GaveNoReason)]
    public override Task Concat_string_int(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Concat_string_int(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4106, 4122, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 4106, 4122, Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Non_nullable_property_through_optional_navigation(bool async)
    {
        // Always throws for sync.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            await AssertQuery(
                async,
                ss => ss.Set<NorthwindCustomer>().Select(e => new { e.Region!.Length }),
                ss => ss.Set<NorthwindCustomer>().Where(e => e.Region != null).Select(e => new { e.Region!.Length }));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4124, 4135, Justification = Upstream.GaveNoReason)]
    public override Task ToList_over_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.ToList_over_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4137, 4149, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_not_null_composite_key(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_not_null_composite_key(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4151, 4152, Justification = Upstream.GaveNoReason)]
    public override void Query_composition_against_ienumerable_set()
        => Fixture.NoSyncTest(base.Query_composition_against_ienumerable_set);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 4166, 4186, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Ternary_should_not_evaluate_both_sides(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 4188, 4199, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_orderby(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_orderby(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4201, 4211, Justification = Upstream.GaveNoReason)]
    public override Task Load_should_track_results(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Load_should_track_results(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4213, 4225, Justification = Upstream.GaveNoReason)]
    public override Task Null_parameter_name_works(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Null_parameter_name_works(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4227, 4248, Justification = Upstream.GaveNoReason)]
    public override Task Where_Property_shadow_closure(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_Property_shadow_closure(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4250, 4264, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_local_double_check(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_local_double_check(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4266, 4278, Justification = Upstream.GaveNoReason)]
    public override Task ToArray_over_string(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.ToArray_over_string(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4280, 4292, Justification = Upstream.GaveNoReason)]
    public override Task MemberInitExpression_NewExpression_is_funcletized_even_when_bindings_are_not_evaluatable(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.MemberInitExpression_NewExpression_is_funcletized_even_when_bindings_are_not_evaluatable(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4294, 4305, Justification = Upstream.GaveNoReason)]
    public override Task Funcletize_conditional_with_evaluatable_test(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Funcletize_conditional_with_evaluatable_test(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4307, 4319, Justification = Upstream.GaveNoReason)]
    public override Task Entity_equality_null_composite_key(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Entity_equality_null_composite_key(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4321, 4335, Justification = Upstream.GaveNoReason)]
    public override Task Concat_parameter_string_int(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Concat_parameter_string_int(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4342, 4354, Justification = Upstream.GaveNoReason)]
    public override Task Where_Property_when_shadow(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_Property_when_shadow(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4356, 4363, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 4356, 4363, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the fixture-created backend database; omit client database creation from upstream setup.")]
    public override async Task Throws_on_concurrent_query_list(bool async)
    {
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        using var context = CreateContext();
        // The fixture creates the backend database; the client has no database to create.

        using var synchronizationEvent = new ManualResetEventSlim(false);
        using var blockingSemaphore = new SemaphoreSlim(0);
        var blockingTask = Task.Run(async () =>
        {
            try
            {
                await context.Customers.Select(c => ProcessCustomer(c, synchronizationEvent, blockingSemaphore)).ToListAsync();
            }
            finally
            {
                synchronizationEvent.Set();
            }
        });

        var throwingTask = Task.Run(async () =>
        {
            synchronizationEvent.Wait(TimeSpan.FromMinutes(5));
            Assert.Equal(
                CoreStrings.ConcurrentMethodInvocation,
                (async
                    ? await Assert.ThrowsAsync<InvalidOperationException>(() => context.Customers.ToListAsync())
                    : Assert.Throws<InvalidOperationException>(() => context.Customers.ToList())).Message);
        });

        await throwingTask;

        blockingSemaphore.Release(1);

        await blockingTask;
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4365, 4380, Justification = Upstream.GaveNoReason)]
    public override Task Convert_to_nullable_on_nullable_value_is_ignored(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await AssertQuery(
                    a,
                    ss => ss.Set<Order>().Select(o => new Order { OrderDate = o.OrderDate!.Value }),
                    elementSorter: e => e.OrderDate);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4382, 4394, Justification = Upstream.GaveNoReason)]
    public override Task Ternary_should_not_evaluate_both_sides_with_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Ternary_should_not_evaluate_both_sides_with_parameter(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4396, 4412, Justification = Upstream.GaveNoReason)]
    public override Task Context_based_client_method(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Context_based_client_method(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4414, 4425, Justification = Upstream.GaveNoReason)]
    public override Task OrderByDescending(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.OrderByDescending(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4427, 4438, Justification = Upstream.GaveNoReason)]
    public override Task Select_Property_when_shadow(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_Property_when_shadow(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4440, 4464, Justification = Upstream.GaveNoReason)]
    public override Task Skip_0_Take_0_works_when_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Skip_0_Take_0_works_when_parameter(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4466, 4467, Skip = true, Justification = "No sync on Cosmos")]
    public override Task Mixed_sync_async_in_query_cache() => throw SkipException.ForSkip("No sync on Cosmos");

    [StoreLimit(UpstreamRepository.EfCore, Source, 4469, 4478, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override async Task Client_code_using_instance_method_throws(bool async)
    {
        Assert.Equal(
            CoreStrings.ClientProjectionCapturingConstantInMethodInstance(
                typeof(NorthwindMiscellaneousTest).FullName!,
                "InstanceMethod"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_method_throws(async))).Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4480, 4489, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override async Task Client_code_using_instance_in_static_method(bool async)
    {
        Assert.Equal(
            CoreStrings.ClientProjectionCapturingConstantInMethodArgument(
                typeof(NorthwindMiscellaneousTest).FullName!,
                "StaticMethod"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_static_method(async))).Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4491, 4499, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override async Task Client_code_using_instance_in_anonymous_type(bool async)
    {
        Assert.Equal(
            CoreStrings.ClientProjectionCapturingConstantInTree(
                typeof(NorthwindMiscellaneousTest).FullName!),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Client_code_using_instance_in_anonymous_type(async))).Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4501, 4510, Justification = Upstream.GaveNoReason, Deviation = DeviationKind.Other, DeviationNote = "Use the matching test and actual adapter or fixture type in the expected diagnostic.")]
    public override async Task Client_code_unknown_method(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Client_code_unknown_method(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.NorthwindMiscellaneousQueryTestBase<InfoCarrier.Core.CosmosTests.Query.NorthwindFixture<Microsoft.EntityFrameworkCore.TestUtilities.NoopModelCustomizer>>",
                nameof(UnknownMethod)));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4554, 4566, Justification = Upstream.GaveNoReason)]
    public override async Task Where_query_composition5(bool async)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => AssertQuery(
            async,
            ss => from c1 in ss.Set<NorthwindCustomer>()
                  where c1.IsLondon == ss.Set<NorthwindCustomer>().OrderBy(c => c.CustomerID).First().IsLondon
                  select c1));

        Assert.Contains("Cosmos subqueries must be correlated, referencing values from the outer query.", exception.Message);
        Assert.Contains(CoreStrings.QueryUnableToTranslateMember(nameof(NorthwindCustomer.IsLondon), "Customer"), exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4568, 4583, Justification = Upstream.GaveNoReason)]
    public override async Task Where_query_composition6(bool async)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => AssertQuery(
            async,
            ss => from c1 in ss.Set<NorthwindCustomer>()
                  where c1.IsLondon
                      == ss.Set<NorthwindCustomer>().OrderBy(c => c.CustomerID)
                          .Select(c => new { Foo = c })
                          .First().Foo.IsLondon
                  select c1));

        Assert.Contains("Cosmos subqueries must be correlated, referencing values from the outer query.", exception.Message);
        Assert.Contains(CoreStrings.QueryUnableToTranslateMember(nameof(NorthwindCustomer.IsLondon), "Customer"), exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4592, 4593, Justification = Upstream.GaveNoReason)]
    public override Task IQueryable_captured_variable()
        => AssertTranslationFailed(() => base.IQueryable_captured_variable());

    [StoreLimit(UpstreamRepository.EfCore, Source, 4623, 4629, Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Entity_equality_through_subquery_composite_key(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Entity_equality_through_subquery_composite_key(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4666, 4672, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_zero(bool async)
    {
        await AssertTranslationFailed(()
            => base.Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_zero(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4674, 4680, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_one(bool async)
    {
        await AssertTranslationFailed(()
            => base.Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_one(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4682, 4688, Justification = Upstream.GaveNoReason)]
    public override async Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_parameter(bool async)
    {
        await AssertTranslationFailed(()
            => base.Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_parameter(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4690, 4695, Justification = Upstream.GaveNoReason)]
    public override async Task Subquery_with_navigation_inside_inline_collection(bool async)
    {
        await AssertTranslationFailed(() => base.Subquery_with_navigation_inside_inline_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4697, 4702, Justification = Upstream.GaveNoReason)]
    public override async Task Parameter_collection_Contains_with_projection_and_ordering(bool async)
    {
        await AssertTranslationFailed(() => base.Parameter_collection_Contains_with_projection_and_ordering(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4704, 4717, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_concatenated_columns_with_different_sizes(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Contains_over_concatenated_columns_with_different_sizes(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4719, 4733, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_concatenated_column_and_constant(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Contains_over_concatenated_column_and_constant(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4735, 4740, Justification = Upstream.GaveNoReason)]
    public override async Task Contains_over_concatenated_columns_both_fixed_length(bool async)
    {
        await AssertTranslationFailed(() => base.Contains_over_concatenated_columns_both_fixed_length(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4742, 4756, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_concatenated_column_and_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Contains_over_concatenated_column_and_parameter(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4758, 4772, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_concatenated_parameter_and_constant(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Contains_over_concatenated_parameter_and_constant(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4774, 4788, Justification = Upstream.GaveNoReason)]
    public override Task Compiler_generated_local_closure_produces_valid_parameter_name(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Compiler_generated_local_closure_produces_valid_parameter_name(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4790, 4797, Justification = Upstream.GaveNoReason)]
    public override Task Static_member_access_gets_parameterized_within_larger_evaluatable(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Static_member_access_gets_parameterized_within_larger_evaluatable(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4799, 4811, Justification = Upstream.GaveNoReason)]
    public override Task Select_Order(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_Order(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4813, 4825, Justification = Upstream.GaveNoReason)]
    public override Task Select_OrderDescending(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Select_OrderDescending(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 4827, 4832, Justification = Upstream.GaveNoReason)]
    public override async Task Where_Order_First(bool async)
    {
        await AssertTranslationFailed(() => base.Where_Order_First(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4834, 4846, Justification = Upstream.GaveNoReason)]
    public override Task Where_nanosecond_and_microsecond_component(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_nanosecond_and_microsecond_component(a);
            });

    [StoreDefect("2.7", UpstreamRepository.EfCore, Source, 4848, 4904, Skip = true, Justification = "MaxItemCount not respected")]
    [Fact]
    public virtual Task ToPageAsync() => throw SkipException.ForSkip("MaxItemCount not respected");

    [StoreDefect("2.7", UpstreamRepository.EfCore, Source, 4906, 4963, Skip = true, Justification = "MaxItemCount not respected")]
    [Fact]
    public virtual Task ToPageAsync_with_scalar() => throw SkipException.ForSkip("MaxItemCount not respected");

    [StoreLimit(UpstreamRepository.EfCore, Source, 4965, 4990, Justification = Upstream.GaveNoReason)]
    [Fact]
    public virtual async Task ToPageAsync_with_exact_maxItemCount()
    {
        await using var context = CreateContext();

        var totalCustomers = await context.Set<NorthwindCustomer>().CountAsync(TestContext.Current.CancellationToken);

        var onlyPage = await context.Set<NorthwindCustomer>()
            .OrderBy(c => c.CustomerID)
            .ToPageAsync(pageSize: totalCustomers, continuationToken: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ALFKI", onlyPage.Values[0].CustomerID);
        Assert.Equal("WOLZA", onlyPage.Values[^1].CustomerID);
        Assert.Null(onlyPage.ContinuationToken);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 4992, 5009, Justification = Upstream.GaveNoReason)]
    [Fact]
    public virtual async Task ToPageAsync_does_not_use_ReadItem()
    {
        await using var context = CreateContext();

        var onlyPage = await context.Set<NorthwindCustomer>()
            .Where(c => c.CustomerID == "ALFKI")
            .ToPageAsync(pageSize: 10, continuationToken: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("ALFKI", onlyPage.Values[0].CustomerID);
        Assert.Null(onlyPage.ContinuationToken);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 5011, 5017, Justification = Upstream.GaveNoReason)]
    [Fact]
    public virtual async Task ToPageAsync_in_subquery_throws()
        => await AssertTranslationFailedWithDetails(
            () => AssertQuery(
                async: true,
                ss => ss.Set<NorthwindCustomer>().Where(c => c.Orders.AsQueryable().ToPageAsync(1, null, null, default) == default)),
            "'ToPageAsync' can only be used as the terminating operator of the top-level query.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 5019, 5035, Justification = Upstream.GaveNoReason)]
    public override Task Ternary_Not_Null_Contains(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Ternary_Not_Null_Contains(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 5037, 5051, Justification = Upstream.GaveNoReason)]
    public override Task Ternary_Not_Null_endsWith_Non_Numeric_First_Part(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Ternary_Not_Null_endsWith_Non_Numeric_First_Part(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 5053, 5067, Justification = Upstream.GaveNoReason)]
    public override Task Ternary_Null_Equals_Non_Numeric_First_Part(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Ternary_Null_Equals_Non_Numeric_First_Part(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 5069, 5083, Justification = Upstream.GaveNoReason)]
    public override Task Ternary_Null_StartsWith(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Ternary_Null_StartsWith(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 5085, 5091, Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Column_access_inside_subquery_predicate(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Column_access_inside_subquery_predicate(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 5093, 5102, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 5093, 5102, Case = "async: True", Justification = "Cosmos doesn't support ORDER BY over parameter/constant:")]
    public override async Task Cast_to_object_over_parameter_directly_in_lambda(bool async)
    {
        // Sync always throws before getting to exception being tested.
        if (!async) { throw SkipException.ForSkip("Always throws for sync."); }
        {
            // Cosmos doesn't support ORDER BY over parameter/constant:
            // Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.
            await AssertStoreException(() => base.Cast_to_object_over_parameter_directly_in_lambda(async: true));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 5104, 5110, Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Late_subquery_pushdown(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Late_subquery_pushdown(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 5112, 5122, Justification = Upstream.GaveNoReason)]
    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual async Task Concat_on_entity_queries_throws(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => AssertQuery(
                async,
                ss => ss.Set<NorthwindCustomer>().Concat(ss.Set<NorthwindCustomer>())),
            "Cosmos subqueries must be correlated, referencing values from the outer query.");
    }
}
#pragma warning restore EF9102

