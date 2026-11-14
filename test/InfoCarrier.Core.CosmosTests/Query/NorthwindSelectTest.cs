// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindSelectTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindSelectQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindSelectQueryCosmosTest.cs";

    // Additional official Cosmos projection scenario.
    [Theory, MemberData(nameof(IsAsyncData))]
    public virtual Task Projection_with_Value_Property(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await AssertQuery(
                    a,
                    ss => ss.Set<Microsoft.EntityFrameworkCore.TestModels.Northwind.Order>().Select(o => new { Value = o.OrderID }),
                    e => e.Value);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 44, 60,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_when_arithmetic_expression_precedence(bool async) => Fixture.NoSyncTest(async, base.Projection_when_arithmetic_expression_precedence);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 62, 88,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Projection_when_arithmetic_expressions(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 90, 96,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_when_arithmetic_mixed(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_when_arithmetic_mixed(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 98, 104,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_when_arithmetic_mixed_subqueries(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_when_arithmetic_mixed_subqueries(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 106, 117,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_when_null_value(bool async) => Fixture.NoSyncTest(async, base.Projection_when_null_value);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 119, 125,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_when_client_evald_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_when_client_evald_subquery(async));
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 127, 144,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Project_to_object_array(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 146, 158,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_of_entity_type_into_object_array(bool async) => Fixture.NoSyncTest(async, base.Projection_of_entity_type_into_object_array);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 160, 166,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_of_multiple_entity_types_into_object_array(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_of_multiple_entity_types_into_object_array(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 168, 179,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_of_entity_type_into_object_list(bool async) => Fixture.NoSyncTest(async, base.Projection_of_entity_type_into_object_list);

    [StoreLimit(UpstreamRepository.EfCore, Source, 181, 193,
        Justification = Upstream.GaveNoReason)]
    public override Task Project_to_int_array(bool async) => Fixture.NoSyncTest(async, base.Project_to_int_array);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 195, 201,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_bool_closure_with_order_by_property_with_cast_to_nullable(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_bool_closure_with_order_by_property_with_cast_to_nullable(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 203, 219,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other | DeviationKind.StoreExceptionAsData,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty. Assert the remote CosmosException type and BadRequest status text.")]
    public override Task Select_bool_closure_with_order_parameter_with_cast_to_nullable(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (!a)
            {
                await base.Select_bool_closure_with_order_parameter_with_cast_to_nullable(false);
                return;
            }
            var error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.Select_bool_closure_with_order_parameter_with_cast_to_nullable(a));
            Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
            Assert.Contains("BadRequest (400)", error.Message, StringComparison.Ordinal);
        });

    [StoreLimit(UpstreamRepository.EfCore, Source, 221, 232,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_scalar(bool async) => Fixture.NoSyncTest(async, base.Select_scalar);

    [StoreLimit(UpstreamRepository.EfCore, Source, 234, 244,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_one(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_one);

    [StoreLimit(UpstreamRepository.EfCore, Source, 246, 257,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_two(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_two);

    [StoreLimit(UpstreamRepository.EfCore, Source, 259, 270,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_three(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_three);

    [StoreLimit(UpstreamRepository.EfCore, Source, 272, 287,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_bool_constant_true(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_bool_constant_true);

    [StoreLimit(UpstreamRepository.EfCore, Source, 289, 303,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_constant_in_expression(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_constant_in_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 305, 321,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_conditional_expression(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_conditional_expression);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 323, 339,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Select_anonymous_with_object(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 341, 351,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_constant_int(bool async) => Fixture.NoSyncTest(async, base.Select_constant_int);

    [StoreLimit(UpstreamRepository.EfCore, Source, 353, 364,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_constant_null_string(bool async) => Fixture.NoSyncTest(async, base.Select_constant_null_string);

    [StoreLimit(UpstreamRepository.EfCore, Source, 366, 378,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_local(bool async) => Fixture.NoSyncTest(async, base.Select_local);

    [StoreLimit(UpstreamRepository.EfCore, Source, 380, 394,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_scalar_primitive_after_take(bool async) => Fixture.NoSyncTest(async, base.Select_scalar_primitive_after_take);

    [StoreLimit(UpstreamRepository.EfCore, Source, 396, 407,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_project_filter(bool async) => Fixture.NoSyncTest(async, base.Select_project_filter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 409, 421,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_project_filter2(bool async) => Fixture.NoSyncTest(async, base.Select_project_filter2);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 423, 429,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 431, 437,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 439, 445,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 447, 453,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level3(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 455, 461,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level4(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level4(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 463, 469,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level5(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level5(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 471, 477,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_multi_level6(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_multi_level6(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 479, 485,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_count_using_anonymous_type(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_count_using_anonymous_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 487, 498,
        Justification = Upstream.GaveNoReason)]
    public override Task New_date_time_in_anonymous_type_works(bool async) => Fixture.NoSyncTest(async, base.New_date_time_in_anonymous_type_works);

    [StoreLimit(UpstreamRepository.EfCore, Source, 500, 513,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_int_to_long_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_int_to_long_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 515, 528,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_nullable_int_to_long_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_nullable_int_to_long_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 530, 543,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_nullable_int_to_int_doesnt_introduce_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_nullable_int_to_int_doesnt_introduce_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 545, 558,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_int_to_nullable_int_doesnt_introduce_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_int_to_nullable_int_doesnt_introduce_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 560, 573,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_binary_expression_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_binary_expression_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 575, 589,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_binary_expression_nested_introduces_top_level_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_binary_expression_nested_introduces_top_level_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 591, 604,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast1(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast1);

    [StoreLimit(UpstreamRepository.EfCore, Source, 606, 619,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast2(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_unary_expression_introduces_explicit_cast2);

    [StoreLimit(UpstreamRepository.EfCore, Source, 621, 634,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_length_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_length_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 636, 649,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_method_call_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_method_call_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 651, 669,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_non_matching_value_types_from_anonymous_type_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Select_non_matching_value_types_from_anonymous_type_introduces_explicit_cast);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 671, 679,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task
        Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault_followed_by_projecting_length(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base
            .Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault_followed_by_projecting_length(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 681, 693,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_conditional_with_null_comparison_in_test(bool async) => Fixture.NoSyncTest(async, base.Select_conditional_with_null_comparison_in_test);

    [StoreLimit(UpstreamRepository.EfCore, Source, 695, 707,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty.")]
    public override Task Projection_in_a_subquery_should_be_liftable(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (!a)
            {
                await base.Projection_in_a_subquery_should_be_liftable(false);
                return;
            }
            Assert.Equal(
                "Cosmos SQL does not allow Offset without Limit. Consider specifying a 'Take' operation on the query.",
                (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Projection_in_a_subquery_should_be_liftable(a)))
                .Message);
        });

    [StoreLimit(UpstreamRepository.EfCore, Source, 709, 710,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_containing_DateTime_subtraction(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Projection_containing_DateTime_subtraction(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 712, 717,
        Justification = Upstream.GaveNoReason)]
    public override async Task Multiple_members_of_correlated_single_result_subquery_lift_to_single_join(bool async, string method)
    {
        await AssertTranslationFailed(() => base.Multiple_members_of_correlated_single_result_subquery_lift_to_single_join(async, method));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 719, 725,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 727, 733,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Skip_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_single_element_from_collection_with_OrderBy_Skip_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 735, 741,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_single_element_from_collection_with_OrderBy_Distinct_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 743, 749,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Take_and_SingleOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_single_element_from_collection_with_OrderBy_Take_and_SingleOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 751, 758,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Take_OrderBy_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_OrderBy_Take_OrderBy_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 760, 767,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault_with_parameter(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_OrderBy_Take_and_FirstOrDefault_with_parameter(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 769, 776,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 778, 788,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task
        Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_followed_by_projection_of_length_property(
            bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base
            .Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_followed_by_projection_of_length_property(
                async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 790, 797,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_multiple_OrderBys_Take_and_FirstOrDefault_2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 799, 806,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 808, 816,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault_2(
        bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Project_single_element_from_collection_with_OrderBy_over_navigation_Take_and_FirstOrDefault_2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 818, 830,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_year_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_year_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 832, 844,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_month_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_month_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 846, 859,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_day_of_year_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_day_of_year_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 861, 873,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_day_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_day_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 875, 887,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_hour_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_hour_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 889, 901,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_minute_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_minute_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 903, 915,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_second_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_second_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 917, 929,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_millisecond_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_millisecond_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 931, 942,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_byte_constant(bool async) => Fixture.NoSyncTest(async, base.Select_byte_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 944, 955,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_short_constant(bool async) => Fixture.NoSyncTest(async, base.Select_short_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 957, 968,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_bool_constant(bool async) => Fixture.NoSyncTest(async, base.Select_bool_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 970, 982,
        Justification = Upstream.GaveNoReason)]
    public override Task Anonymous_projection_AsNoTracking_Selector(bool async) => Fixture.NoSyncTest(async, base.Anonymous_projection_AsNoTracking_Selector);

    [StoreLimit(UpstreamRepository.EfCore, Source, 984, 995,
        Justification = Upstream.GaveNoReason)]
    public override Task Anonymous_projection_with_repeated_property_being_ordered(bool async) => Fixture.NoSyncTest(async, base.Anonymous_projection_with_repeated_property_being_ordered);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 997, 1003,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Anonymous_projection_with_repeated_property_being_ordered_2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Anonymous_projection_with_repeated_property_being_ordered_2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1005, 1017,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_GetValueOrDefault_on_DateTime(bool async) => Fixture.NoSyncTest(async, base.Select_GetValueOrDefault_on_DateTime);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1019, 1025,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_GetValueOrDefault_on_DateTime_with_null_values(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_GetValueOrDefault_on_DateTime_with_null_values(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1027, 1038,
        Justification = Upstream.GaveNoReason)]
    public override Task Client_method_in_projection_requiring_materialization_1(bool async) => Fixture.NoSyncTest(async, base.Client_method_in_projection_requiring_materialization_1);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1040, 1051,
        Justification = Upstream.GaveNoReason)]
    public override Task Client_method_in_projection_requiring_materialization_2(bool async) => Fixture.NoSyncTest(async, base.Client_method_in_projection_requiring_materialization_2);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1053, 1059,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Multiple_select_many_with_predicate(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Multiple_select_many_with_predicate(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1061, 1067,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_without_result_selector_naked_collection_navigation(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_without_result_selector_naked_collection_navigation(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1069, 1075,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_without_result_selector_collection_navigation_composed(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_without_result_selector_collection_navigation_composed(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1077, 1083,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_1(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1085, 1095,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_over_inline_array_projecting_range_variable_and_outer(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.SelectMany_over_inline_array_projecting_range_variable_and_outer(async));

        Assert.Equal("Complex projections in subqueries are currently unsupported.", exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1097, 1103,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1105, 1111,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_3(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1113, 1119,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_4(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_4(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1121, 1127,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_5(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_5(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1129, 1135,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_6(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_6(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1137, 1143,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_correlated_with_outer_7(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_correlated_with_outer_7(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1145, 1151,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_with_multiple_Take(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_with_multiple_Take(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1153, 1159,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_with_nested_DefaultIfEmpty(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_with_nested_DefaultIfEmpty(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1161, 1167,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_with_multiple_Take(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_with_multiple_Take(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1169, 1175,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task FirstOrDefault_over_empty_collection_of_value_type_returns_correct_results(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.FirstOrDefault_over_empty_collection_of_value_type_returns_correct_results(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1177, 1183,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_non_nullable_value_after_FirstOrDefault_on_empty_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_non_nullable_value_after_FirstOrDefault_on_empty_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1185, 1190,
        Justification = Upstream.GaveNoReason)]
    public override async Task Member_binding_after_ctor_arguments_fails_with_client_eval(bool async)
    {
        await AssertTranslationFailed(() => base.Member_binding_after_ctor_arguments_fails_with_client_eval(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1192, 1198,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Filtered_collection_projection_is_tracked(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Filtered_collection_projection_is_tracked(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1200, 1206,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Filtered_collection_projection_with_to_list_is_tracked(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Filtered_collection_projection_with_to_list_is_tracked(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1208, 1215,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.SelectMany_with_collection_being_correlated_subquery_which_references_inner_and_outer_entity(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1217, 1223,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_chained_entity_navigation_doesnt_materialize_intermittent_entities(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_chained_entity_navigation_doesnt_materialize_intermittent_entities(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1225, 1231,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_entity_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_entity_compared_to_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1233, 1245,
        Justification = Upstream.GaveNoReason)]
    public override Task Explicit_cast_in_arithmetic_operation_is_preserved(bool async) => Fixture.NoSyncTest(async, base.Explicit_cast_in_arithmetic_operation_is_preserved);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1247, 1253,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SelectMany_whose_selector_references_outer_source(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SelectMany_whose_selector_references_outer_source(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1255, 1261,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Collection_FirstOrDefault_with_entity_equality_check_in_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_FirstOrDefault_with_entity_equality_check_in_projection(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1263, 1269,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Collection_FirstOrDefault_with_nullable_unsigned_int_column(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_FirstOrDefault_with_nullable_unsigned_int_column(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1271, 1277,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task ToList_Count_in_projection_works(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.ToList_Count_in_projection_works(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1279, 1285,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task LastOrDefault_member_access_in_projection_translates_to_server(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.LastOrDefault_member_access_in_projection_translates_to_server(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1287, 1293,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Collection_projection_AsNoTracking_OrderBy(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_projection_AsNoTracking_OrderBy(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1295, 1307,
        Justification = Upstream.GaveNoReason)]
    public override Task Coalesce_over_nullable_uint(bool async) => Fixture.NoSyncTest(async, base.Coalesce_over_nullable_uint);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1309, 1315,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_uint_through_collection_FirstOrDefault(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_uint_through_collection_FirstOrDefault(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1317, 1323,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Project_keyless_entity_FirstOrDefault_without_orderby(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Project_keyless_entity_FirstOrDefault_without_orderby(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1325, 1337,
        Justification = Upstream.GaveNoReason)]
    public override Task Reverse_changes_asc_order_to_desc(bool async) => Fixture.NoSyncTest(async, base.Reverse_changes_asc_order_to_desc);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1339, 1351,
        Justification = Upstream.GaveNoReason)]
    public override Task Reverse_changes_desc_order_to_asc(bool async) => Fixture.NoSyncTest(async, base.Reverse_changes_desc_order_to_asc);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1353, 1359,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projection_AsEnumerable_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projection_AsEnumerable_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1361, 1373,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_custom_type_in_both_sides_of_ternary(bool async) => Fixture.NoSyncTest(async, base.Projection_custom_type_in_both_sides_of_ternary);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1375, 1381,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projecting_multiple_collection_with_same_constant_works(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projecting_multiple_collection_with_same_constant_works(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1383, 1389,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projecting_after_navigation_and_distinct(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projecting_after_navigation_and_distinct(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1391, 1398,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Correlated_collection_after_distinct_with_complex_projection_containing_original_identifier(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1400, 1406,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Correlated_collection_after_distinct_not_containing_original_identifier(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Correlated_collection_after_distinct_not_containing_original_identifier(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1408, 1415,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Correlated_collection_after_distinct_with_complex_projection_not_containing_original_identifier(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1417, 1424,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Correlated_collection_after_groupby_with_complex_projection_containing_original_identifier(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1426, 1432,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_without_explicit_ordering(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Reverse_without_explicit_ordering(async), "'Reverse' could not be translated to the server because there is no ordering on the server side.");
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1434, 1440,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Custom_projection_reference_navigation_PK_to_FK_optimization(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Custom_projection_reference_navigation_PK_to_FK_optimization(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1442, 1448,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_deep(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_deep(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1450, 1457,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projecting_Length_of_a_string_property_after_FirstOrDefault_on_correlated_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Projecting_Length_of_a_string_property_after_FirstOrDefault_on_correlated_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1459, 1462,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_take_predicate_projection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Projection_take_predicate_projection(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1464, 1478,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_take_projection_doesnt_project_intermittent_column(bool async) => Fixture.NoSyncTest(async, base.Projection_take_projection_doesnt_project_intermittent_column);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1480, 1490,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty.")]
    public override Task Projection_skip_projection_doesnt_project_intermittent_column(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (!a)
            {
                await base.Projection_skip_projection_doesnt_project_intermittent_column(false);
                return;
            }
            var message = (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Projection_skip_projection_doesnt_project_intermittent_column(a))).Message;

            Assert.Equal("Cosmos SQL does not allow Offset without Limit. Consider specifying a 'Take' operation on the query.", message);
        });

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1492, 1502,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Assert translation refusal in both modes; upstream leaves the synchronous arm empty. A direct-store control checks both modes.")]
    public override Task Projection_Distinct_projection_preserves_columns_used_for_distinct_in_subquery(bool async)
        => AssertTranslationFailed(() => base.Projection_Distinct_projection_preserves_columns_used_for_distinct_in_subquery(async));


    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1504, 1510,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projecting_count_of_navigation_which_is_generic_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projecting_count_of_navigation_which_is_generic_collection(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1512, 1518,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Projecting_count_of_navigation_which_is_generic_list(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Projecting_count_of_navigation_which_is_generic_list(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1520, 1531,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Do_not_erase_projection_mapping_when_adding_single_projection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        Assert.Equal(
            "Including navigation 'Navigation: Order.OrderDetails (ICollection<OrderDetail>) Collection ToDependent OrderDetail Inverse: Order' is not supported as the navigation is not embedded in same resource.",
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Do_not_erase_projection_mapping_when_adding_single_projection(async)))
            .Message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1533, 1539,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Select_nested_collection_deep_distinct_no_identifiers(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Select_nested_collection_deep_distinct_no_identifiers(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1541, 1548,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(()
            => base.Correlated_collection_after_groupby_with_complex_projection_not_containing_original_identifier(async));
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 1550, 1575,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Ternary_in_client_eval_assigns_correct_types(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1577, 1587,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Collection_include_over_result_of_single_non_scalar(bool async)
    {
        // Cross collection join. Issue #17246.
        Assert.Equal(
            "Including navigation 'Navigation: Customer.Orders (List<Order>) Collection ToDependent Order Inverse: Customer PropertyAccessMode.Field' is not supported as the navigation is not embedded in same resource.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Collection_include_over_result_of_single_non_scalar(async)))
            .Message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1589, 1595,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Collection_projection_selecting_outer_element_followed_by_take(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_projection_selecting_outer_element_followed_by_take(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1597, 1603,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Take_on_top_level_and_on_collection_projection_with_outer_apply(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Take_on_top_level_and_on_collection_projection_with_outer_apply(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1605, 1611,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Take_on_correlated_collection_in_first(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Take_on_correlated_collection_in_first(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1613, 1619,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Client_projection_via_ctor_arguments(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Client_projection_via_ctor_arguments(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1621, 1627,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Client_projection_with_string_initialization_with_scalar_subquery(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Client_projection_with_string_initialization_with_scalar_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1629, 1640,
        Justification = Upstream.GaveNoReason)]
    public override async Task Projecting_count_of_navigation_which_is_generic_collection_using_convert(bool async)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Projecting_count_of_navigation_which_is_generic_collection_using_convert(async));

        Assert.Equal(
            "Including navigation 'Navigation: Customer.Orders (List<Order>) Collection ToDependent Order Inverse: Customer PropertyAccessMode.Field' is not supported as the navigation is not embedded in same resource.",
            ex.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1642, 1648,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task MemberInit_in_projection_without_arguments(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.MemberInit_in_projection_without_arguments(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1650, 1656,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Reverse_in_join_outer(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_join_outer(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1658, 1664,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Reverse_in_join_outer_with_take(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_join_outer_with_take(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1666, 1672,
        Justification = "Cross collection join. Issue #17246.")]
    public override async Task Reverse_in_join_inner(bool async)
    {
        // Cross collection join. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_join_inner(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1674, 1681,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_in_join_inner_with_skip(bool async)
    {
        await AssertTranslationFailedWithDetails(
            () => base.Reverse_in_join_inner_with_skip(async),
            "Root entity type 'Order' is referenced by the query, but 'Customer' is already being referenced. A query can only reference a single root entity type.");
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1683, 1689,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_SelectMany(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_SelectMany(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1691, 1697,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_SelectMany_with_Take(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_SelectMany_with_Take(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1699, 1705,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_projection_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_projection_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1707, 1713,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_projection_subquery_single_result(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_projection_subquery_single_result(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1715, 1721,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_subquery_via_pushdown(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_subquery_via_pushdown(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1723, 1729,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Reverse_in_projection_scalar_subquery(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Reverse_in_projection_scalar_subquery(async));
    }

    [StoreDefect("2.4", UpstreamRepository.EfCore, Source, 1731, 1748,
        Skip = true, Justification = "ORDER BY with expressions/functions not supported")]
    public override Task Reverse_after_orderby_thenby(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1750, 1755,
        Justification = Upstream.GaveNoReason)]
    public override async Task Reverse_after_orderBy_and_take(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Reverse_after_orderBy_and_take(async))).Message;

        Assert.Equal("Reversing the ordering is not supported when limit or offset are already applied.", message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1757, 1763,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task List_of_list_of_anonymous_type(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.List_of_list_of_anonymous_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1765, 1774,
        Justification = Upstream.GaveNoReason)]
    public override async Task
        SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
            bool async)
    {
        await AssertTranslationFailed(() => base
            .SelectMany_with_collection_being_correlated_subquery_which_references_non_mapped_properties_from_inner_and_outer_entity(
                async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1776, 1791,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_bool_closure(bool async) => Fixture.NoSyncTest(async, base.Select_bool_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1793, 1805,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_DayOfWeek_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_DayOfWeek_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1807, 1819,
        Justification = Upstream.GaveNoReason)]
    public override Task Reverse_after_multiple_orderbys(bool async) => Fixture.NoSyncTest(async, base.Reverse_after_multiple_orderbys);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1821, 1836,
        Skip = true, Justification = "Always does sync evaluation.")]
    public override Task VisitLambda_should_not_be_visited_trivially(bool async) => throw SkipException.ForSkip("Always does sync evaluation.");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 1838, 1855,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Projecting_nullable_struct(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 1857, 1868,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_customer_identity(bool async) => Fixture.NoSyncTest(async, base.Select_customer_identity);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1870, 1882,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_with_parameterized_constructor(bool async) => Fixture.NoSyncTest(async, base.Projection_with_parameterized_constructor);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1884, 1895,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_nested(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_nested);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1897, 1909,
        Justification = Upstream.GaveNoReason)]
    public override Task Cast_on_top_level_projection_brings_explicit_Cast(bool async) => Fixture.NoSyncTest(async, base.Cast_on_top_level_projection_brings_explicit_Cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1911, 1921,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_empty(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_empty);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1923, 1934,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_scalar_primitive(bool async) => Fixture.NoSyncTest(async, base.Select_scalar_primitive);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1936, 1947,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_into(bool async) => Fixture.NoSyncTest(async, base.Select_into);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1949, 1961,
        Justification = Upstream.GaveNoReason)]
    public override Task Projection_with_parameterized_constructor_with_member_assignment(bool async) => Fixture.NoSyncTest(async, base.Projection_with_parameterized_constructor_with_member_assignment);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1963, 1975,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_TimeOfDay_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_TimeOfDay_component);

    [StoreDefect("1.20", UpstreamRepository.EfCore, Source, 1977, 1991,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.Other,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty.")]
    public override Task Select_with_complex_expression_that_can_be_funcletized(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (!a)
            {
                await base.Select_with_complex_expression_that_can_be_funcletized(false);
                return;
            }
            await Assert.ThrowsAsync<EqualException>(() => base.Select_with_complex_expression_that_can_be_funcletized(true));
        });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1993, 2005,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_datetime_Ticks_component(bool async) => Fixture.NoSyncTest(async, base.Select_datetime_Ticks_component);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2007, 2018,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_anonymous_literal(bool async) => Fixture.NoSyncTest(async, base.Select_anonymous_literal);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2020, 2031,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_customer_table(bool async) => Fixture.NoSyncTest(async, base.Select_customer_table);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2033, 2043,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_over_10_nested_ternary_condition(bool async) => Fixture.NoSyncTest(async, base.Select_over_10_nested_ternary_condition);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2045, 2057,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_conditional_drops_false(bool async) => Fixture.NoSyncTest(async, base.Select_conditional_drops_false);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2059, 2071,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_conditional_terminates_at_true(bool async) => Fixture.NoSyncTest(async, base.Select_conditional_terminates_at_true);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2073, 2085,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_conditional_flatten_nested_results(bool async) => Fixture.NoSyncTest(async, base.Select_conditional_flatten_nested_results);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2087, 2099,
        Justification = Upstream.GaveNoReason)]
    public override Task Select_conditional_flatten_nested_tests(bool async) => Fixture.NoSyncTest(async, base.Select_conditional_flatten_nested_tests);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2101, 2113,
        Justification = Upstream.GaveNoReason)]
    public override Task Using_enumerable_parameter_in_projection(bool async) => Fixture.NoSyncTest(async, base.Using_enumerable_parameter_in_projection);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2115, 2117,
        Skip = true, Justification = "Cross collection join Issue#17246")]
    public override Task List_from_result_of_single_result(bool async) => throw SkipException.ForSkip("Cross collection join Issue#17246");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2119, 2121,
        Skip = true, Justification = "Cross collection join Issue#17246")]
    public override Task List_from_result_of_single_result_2(bool async) => throw SkipException.ForSkip("Cross collection join Issue#17246");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2123, 2125,
        Skip = true, Justification = "Cross collection join Issue#17246")]
    public override Task List_from_result_of_single_result_3(bool async) => throw SkipException.ForSkip("Cross collection join Issue#17246");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2127, 2138,
        Justification = Upstream.GaveNoReason)]
    public override Task Entity_passed_to_DTO_constructor_works(bool async) => Fixture.NoSyncTest(async, base.Entity_passed_to_DTO_constructor_works);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2140, 2146,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Set_operation_in_pending_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Set_operation_in_pending_collection(async));
    }

}
