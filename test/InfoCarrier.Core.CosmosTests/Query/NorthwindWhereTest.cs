// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindWhereTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindWhereQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindWhereQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 36,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple(bool async) => Fixture.NoSyncTest(async, base.Where_simple);

    [StoreLimit(UpstreamRepository.EfCore, Source, 40, 46,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Where_as_queryable_expression(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Where_as_queryable_expression(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 48, 66,
        Justification = Upstream.GaveNoReason)]
    public override async Task<string> Where_simple_closure(bool async)
    {
        await Fixture.NoSyncTest(
            async, async a =>
            {
                var queryString = await base.Where_simple_closure(a);
            });

        return null!;
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 68, 82,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_indexer_closure(bool async) => Fixture.NoSyncTest(async, base.Where_indexer_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 84, 98,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_dictionary_key_access_closure(bool async) => Fixture.NoSyncTest(async, base.Where_dictionary_key_access_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 100, 114,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_tuple_item_closure(bool async) => Fixture.NoSyncTest(async, base.Where_tuple_item_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 116, 130,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_named_tuple_item_closure(bool async) => Fixture.NoSyncTest(async, base.Where_named_tuple_item_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 132, 146,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_closure_constant(bool async) => Fixture.NoSyncTest(async, base.Where_simple_closure_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 148, 170,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_simple_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 172, 194,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_method_call_nullable_type_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_method_call_nullable_type_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 196, 218,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_method_call_nullable_type_reverse_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_method_call_nullable_type_reverse_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 220, 242,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_method_call_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_method_call_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 244, 266,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_field_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_field_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 268, 290,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_property_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_property_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 292, 314,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_static_field_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_static_field_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 316, 338,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_static_property_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_static_property_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 340, 362,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_nested_field_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_nested_field_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 364, 386,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_nested_property_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_nested_property_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 388, 410,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_new_instance_field_access_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_new_instance_field_access_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 412, 434,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_new_instance_field_access_closure_via_query_cache(bool async) => Fixture.NoSyncTest(async, base.Where_new_instance_field_access_closure_via_query_cache);

    [StoreLimit(UpstreamRepository.EfCore, Source, 436, 466,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_closure_via_query_cache_nullable_type(bool async) => Fixture.NoSyncTest(async, base.Where_simple_closure_via_query_cache_nullable_type);

    [StoreLimit(UpstreamRepository.EfCore, Source, 468, 498,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_closure_via_query_cache_nullable_type_reverse(bool async) => Fixture.NoSyncTest(async, base.Where_simple_closure_via_query_cache_nullable_type_reverse);

    [StoreLimit(UpstreamRepository.EfCore, Source, 500, 502, Skip = true, Justification = "Always uses sync code.")]
    public override Task Where_subquery_closure_via_query_cache(bool async) => throw SkipException.ForSkip("Always uses sync code.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 504, 516,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_shadow(bool async) => Fixture.NoSyncTest(async, base.Where_simple_shadow);

    [StoreLimit(UpstreamRepository.EfCore, Source, 518, 530,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_shadow_projection(bool async) => Fixture.NoSyncTest(async, base.Where_simple_shadow_projection);

    [StoreLimit(UpstreamRepository.EfCore, Source, 532, 535,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_shadow_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Where_simple_shadow_subquery(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 537, 543,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Where_shadow_subquery_FirstOrDefault(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Where_shadow_subquery_FirstOrDefault(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 552, 558,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Where_subquery_correlated(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Where_subquery_correlated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 595, 602,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_method_int(bool async) => Fixture.NoSyncTest(async, base.Where_equals_method_int);

    [StoreLimit(UpstreamRepository.EfCore, Source, 604, 616,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_using_object_overload_on_mismatched_types(bool async) => Fixture.NoSyncTest(async, base.Where_equals_using_object_overload_on_mismatched_types);

    [StoreLimit(UpstreamRepository.EfCore, Source, 618, 625,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_using_int_overload_on_mismatched_types(bool async) => Fixture.NoSyncTest(async, base.Where_equals_using_int_overload_on_mismatched_types);

    [StoreLimit(UpstreamRepository.EfCore, Source, 627, 645,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_on_mismatched_types_nullable_int_long(bool async) => Fixture.NoSyncTest(async, base.Where_equals_on_mismatched_types_nullable_int_long);

    [StoreLimit(UpstreamRepository.EfCore, Source, 647, 665,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_on_mismatched_types_nullable_long_nullable_int(bool async) => Fixture.NoSyncTest(async, base.Where_equals_on_mismatched_types_nullable_long_nullable_int);

    [StoreLimit(UpstreamRepository.EfCore, Source, 667, 689,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_on_mismatched_types_int_nullable_int(bool async) => Fixture.NoSyncTest(async, base.Where_equals_on_mismatched_types_int_nullable_int);

    [StoreLimit(UpstreamRepository.EfCore, Source, 691, 713,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_on_matched_nullable_int_types(bool async) => Fixture.NoSyncTest(async, base.Where_equals_on_matched_nullable_int_types);

    [StoreLimit(UpstreamRepository.EfCore, Source, 715, 737,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_equals_on_null_nullable_int_types(bool async) => Fixture.NoSyncTest(async, base.Where_equals_on_null_nullable_int_types);

    [StoreLimit(UpstreamRepository.EfCore, Source, 739, 751,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_comparison_nullable_type_not_null(bool async) => Fixture.NoSyncTest(async, base.Where_comparison_nullable_type_not_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 753, 765,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_comparison_nullable_type_null(bool async) => Fixture.NoSyncTest(async, base.Where_comparison_nullable_type_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 767, 779,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_reversed(bool async) => Fixture.NoSyncTest(async, base.Where_simple_reversed);

    [StoreLimit(UpstreamRepository.EfCore, Source, 781, 793,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_is_null(bool async) => Fixture.NoSyncTest(async, base.Where_is_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 795, 806,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_null_is_null(bool async) => Fixture.NoSyncTest(async, base.Where_null_is_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 808, 820,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_constant_is_null(bool async) => Fixture.NoSyncTest(async, base.Where_constant_is_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 822, 833,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_is_not_null(bool async) => Fixture.NoSyncTest(async, base.Where_is_not_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 835, 847,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_null_is_not_null(bool async) => Fixture.NoSyncTest(async, base.Where_null_is_not_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 849, 859,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_constant_is_not_null(bool async) => Fixture.NoSyncTest(async, base.Where_constant_is_not_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 861, 873,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_identity_comparison(bool async) => Fixture.NoSyncTest(async, base.Where_identity_comparison);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 875, 881,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_in_optimization_multiple(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_in_optimization_multiple(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 883, 889,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_not_in_optimization1(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_not_in_optimization1(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 891, 897,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_not_in_optimization2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_not_in_optimization2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 899, 905,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_not_in_optimization3(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_not_in_optimization3(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 907, 913,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_not_in_optimization4(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_not_in_optimization4(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 915, 921,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_select_many_and(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_select_many_and(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 923, 926,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_primitive(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Where_primitive(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 928, 940,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member);

    [StoreLimit(UpstreamRepository.EfCore, Source, 942, 954,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_false(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_false);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 956, 962,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_bool_client_side_negated(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_bool_client_side_negated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 964, 976,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_negated_twice(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_negated_twice);

    [StoreLimit(UpstreamRepository.EfCore, Source, 978, 990,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_shadow(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_shadow);

    [StoreLimit(UpstreamRepository.EfCore, Source, 992, 1004,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_false_shadow(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_false_shadow);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1006, 1018,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_equals_constant(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_equals_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1020, 1032,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_in_complex_predicate(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_in_complex_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1034, 1046,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_compared_to_binary_expression(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_compared_to_binary_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1048, 1060,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_not_bool_member_compared_to_not_bool_member(bool async) => Fixture.NoSyncTest(async, base.Where_not_bool_member_compared_to_not_bool_member);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1062, 1074,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_negated_boolean_expression_compared_to_another_negated_boolean_expression(bool async) => Fixture.NoSyncTest(async, base.Where_negated_boolean_expression_compared_to_another_negated_boolean_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1076, 1088,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_not_bool_member_compared_to_binary_expression(bool async) => Fixture.NoSyncTest(async, base.Where_not_bool_member_compared_to_binary_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1090, 1104,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_parameter(bool async) => Fixture.NoSyncTest(async, base.Where_bool_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1106, 1120,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_parameter_compared_to_binary_expression(bool async) => Fixture.NoSyncTest(async, base.Where_bool_parameter_compared_to_binary_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1122, 1136,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_member_and_parameter_compared_to_binary_expression_nested(bool async) => Fixture.NoSyncTest(async, base.Where_bool_member_and_parameter_compared_to_binary_expression_nested);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1138, 1150,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_de_morgan_or_optimized(bool async) => Fixture.NoSyncTest(async, base.Where_de_morgan_or_optimized);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1152, 1164,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_de_morgan_and_optimized(bool async) => Fixture.NoSyncTest(async, base.Where_de_morgan_and_optimized);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1166, 1178,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_complex_negated_expression_optimized(bool async) => Fixture.NoSyncTest(async, base.Where_complex_negated_expression_optimized);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1180, 1192,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_short_member_comparison(bool async) => Fixture.NoSyncTest(async, base.Where_short_member_comparison);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1194, 1205,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_comparison_to_nullable_bool(bool async) => Fixture.NoSyncTest(async, base.Where_comparison_to_nullable_bool);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1207, 1217,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_true(bool async) => Fixture.NoSyncTest(async, base.Where_true);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1219, 1231,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_false(bool async) => Fixture.NoSyncTest(async, base.Where_false);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1233, 1253,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_bool_closure(bool async) => Fixture.NoSyncTest(async, base.Where_bool_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1255, 1267,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_default(bool async) => Fixture.NoSyncTest(async, base.Where_default);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1269, 1276,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_expression_invoke_1(bool async) => Fixture.NoSyncTest(async, base.Where_expression_invoke_1);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1278, 1284,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_expression_invoke_2(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_expression_invoke_2(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1286, 1293,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_expression_invoke_3(bool async) => Fixture.NoSyncTest(async, base.Where_expression_invoke_3);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1295, 1307,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_true(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_true);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1309, 1321,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_false(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_false);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1323, 1337,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_with_another_condition(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_with_another_condition);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1339, 1351,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_with_false_as_result_true(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_with_false_as_result_true);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1353, 1365,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_with_false_as_result_false(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_with_false_as_result_false);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1367, 1379,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_ternary_boolean_condition_negated(bool async) => Fixture.NoSyncTest(async, base.Where_ternary_boolean_condition_negated);

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1381, 1387,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_constructed_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1389, 1395,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_constructed_multi_value_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1397, 1403,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_constructed_multi_value_not_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1405, 1411,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_constructed_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1413, 1419,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_constructed_multi_value_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1421, 1427,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_not_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1429, 1435,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_create_constructed_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1437, 1443,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));
    }

    [StoreIssue(IssueTracker.EfCore, 14672, UpstreamRepository.EfCore, Source, 1445, 1451,
        Justification = "Anonymous type to constant comparison. Issue #14672.")]
    public override async Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
    {
        // Anonymous type to constant comparison. Issue #14672.
        await AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1453, 1465,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_compare_null(bool async) => Fixture.NoSyncTest(async, base.Where_compare_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1467, 1478,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Is_on_same_type(bool async) => Fixture.NoSyncTest(async, base.Where_Is_on_same_type);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1480, 1492,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_chain(bool async) => Fixture.NoSyncTest(async, base.Where_chain);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1494, 1504,
        Justification = Upstream.GaveNoReason)]
    public override async Task Where_navigation_contains(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_navigation_contains(async))).Message;

        Assert.Equal(
            "Including navigation 'Navigation: Customer.Orders (List<Order>) Collection ToDependent Order Inverse: Customer PropertyAccessMode.Field' is not supported as the navigation is not embedded in same resource.",
            message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1506, 1513,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_array_index(bool async) => Fixture.NoSyncTest(async, base.Where_array_index);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1515, 1522,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_multiple_contains_in_subquery_with_or(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_multiple_contains_in_subquery_with_or(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1524, 1530,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_multiple_contains_in_subquery_with_and(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_multiple_contains_in_subquery_with_and(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1532, 1538,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_contains_on_navigation(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_contains_on_navigation(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1540, 1546,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Where_subquery_FirstOrDefault_is_null(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Where_subquery_FirstOrDefault_is_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1548, 1554,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_subquery_FirstOrDefault_compared_to_entity(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_subquery_FirstOrDefault_compared_to_entity(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1556, 1570,
        Justification = Upstream.GaveNoReason)]
    public override Task TypeBinary_short_circuit(bool async) => Fixture.NoSyncTest(async, base.TypeBinary_short_circuit);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1572, 1584,
        Justification = Upstream.GaveNoReason)]
    public override Task Decimal_cast_to_double_works(bool async) => Fixture.NoSyncTest(async, base.Decimal_cast_to_double_works);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1586, 1598,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_is_conditional(bool async) => Fixture.NoSyncTest(async, base.Where_is_conditional);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1600, 1606,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task Filter_non_nullable_value_after_FirstOrDefault_on_empty_collection(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.Filter_non_nullable_value_after_FirstOrDefault_on_empty_collection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1608, 1622,
        Justification = Upstream.GaveNoReason)]
    public override Task Using_same_parameter_twice_in_query_generates_one_sql_parameter(bool async) => Fixture.NoSyncTest(async, base.Using_same_parameter_twice_in_query_generates_one_sql_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1624, 1639,
        Justification = Upstream.GaveNoReason)]
    public override Task Two_parameters_with_same_name_get_uniquified(bool async) => Fixture.NoSyncTest(async, base.Two_parameters_with_same_name_get_uniquified);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1641, 1656,
        Justification = Upstream.GaveNoReason)]
    public override Task Two_parameters_with_same_case_insensitive_name_get_uniquified(bool async) => Fixture.NoSyncTest(async, base.Two_parameters_with_same_case_insensitive_name_get_uniquified);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1658, 1664,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToList_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToList_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1666, 1672,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToList_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToList_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1674, 1680,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToArray_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToArray_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1682, 1688,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToArray_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToArray_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1690, 1696,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_AsEnumerable_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_AsEnumerable_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1698, 1704,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_AsEnumerable_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_AsEnumerable_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1706, 1712,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToList_Count_member(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToList_Count_member(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1714, 1720,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_ToArray_Length_member(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_ToArray_Length_member(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1722, 1728,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToList_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToList_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1730, 1736,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToList_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToList_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1738, 1744,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToArray_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToArray_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1746, 1752,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToArray_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToArray_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1754, 1760,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_AsEnumerable_Count(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_AsEnumerable_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1762, 1768,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_AsEnumerable_Contains(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_AsEnumerable_Contains(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1770, 1776,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToList_Count_member(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToList_Count_member(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1778, 1784,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_collection_navigation_ToArray_Length_member(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_collection_navigation_ToArray_Length_member(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1786, 1792,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Where_Queryable_AsEnumerable_Contains_negated(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Where_Queryable_AsEnumerable_Contains_negated(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1794, 1818,
        Case = "withNull: False", Justification = "Cosmos client evaluation. Issue #17246.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 1794, 1818,
        Case = "withNull: True", Justification = Upstream.GaveNoReason)]
    public override async Task Where_Queryable_conditional_not_null_check_with_Contains(bool async, bool withNull)
    {
        if (withNull)
        {
            await Fixture.NoSyncTest(
                async, async a =>
                {
                    await base.Where_Queryable_conditional_not_null_check_with_Contains(a, withNull);
                });
        }
        else
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertTranslationFailed(() => base.Where_Queryable_conditional_not_null_check_with_Contains(async, withNull));
        }
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1820, 1843,
        Case = "withNull: False", Justification = "Cosmos client evaluation. Issue #17246.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 1820, 1843,
        Case = "withNull: True", Justification = Upstream.GaveNoReason)]
    public override async Task Where_Queryable_conditional_null_check_with_Contains(bool async, bool withNull)
    {
        if (withNull)
        {
            await Fixture.NoSyncTest(
                async, async a =>
                {
                    await base.Where_Queryable_conditional_null_check_with_Contains(a, withNull);
                });
        }
        else
        {
            // Cosmos client evaluation. Issue #17246.
            await AssertTranslationFailed(() => base.Where_Queryable_conditional_null_check_with_Contains(async, withNull));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1845, 1871,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Enumerable_conditional_not_null_check_with_Contains(bool async, bool withNull)
        => Fixture.NoSyncTest(async, a => base.Where_Enumerable_conditional_not_null_check_with_Contains(a, withNull));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1873, 1898,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Enumerable_conditional_null_check_with_Contains(bool async, bool withNull)
        => Fixture.NoSyncTest(async, a => base.Where_Enumerable_conditional_null_check_with_Contains(a, withNull));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1900, 1914,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_list_object_contains_over_value_type(bool async) => Fixture.NoSyncTest(async, base.Where_list_object_contains_over_value_type);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1916, 1930,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_array_of_object_contains_over_value_type(bool async) => Fixture.NoSyncTest(async, base.Where_array_of_object_contains_over_value_type);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1932, 1939,
        Justification = Upstream.GaveNoReason)]
    public override Task Filter_with_EF_Property_using_closure_for_property_name(bool async) => Fixture.NoSyncTest(async, base.Filter_with_EF_Property_using_closure_for_property_name);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1941, 1948,
        Justification = Upstream.GaveNoReason)]
    public override Task Filter_with_EF_Property_using_function_for_property_name(bool async) => Fixture.NoSyncTest(async, base.Filter_with_EF_Property_using_function_for_property_name);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1950, 1956,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task FirstOrDefault_over_scalar_projection_compared_to_null(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.FirstOrDefault_over_scalar_projection_compared_to_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1958, 1964,
        Justification = "Uncorrelated subquery, not supported by Cosmos")]
    public override async Task FirstOrDefault_over_scalar_projection_compared_to_not_null(bool async)
    {
        // Uncorrelated subquery, not supported by Cosmos
        await AssertTranslationFailed(() => base.FirstOrDefault_over_scalar_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1966, 1972,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task FirstOrDefault_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.FirstOrDefault_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1974, 1980,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task FirstOrDefault_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.FirstOrDefault_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1982, 1988,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SingleOrDefault_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SingleOrDefault_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1990, 1996,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task SingleOrDefault_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.SingleOrDefault_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1998, 2004,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task LastOrDefault_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.LastOrDefault_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2006, 2012,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task LastOrDefault_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.LastOrDefault_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2014, 2020,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task First_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.First_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2022, 2028,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task First_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.First_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2030, 2036,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task ElementAt_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.ElementAt_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2038, 2044,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task ElementAtOrDefault_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.ElementAtOrDefault_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2046, 2052,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Single_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Single_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2054, 2060,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Single_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Single_over_custom_projection_compared_to_not_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2062, 2068,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Last_over_custom_projection_compared_to_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Last_over_custom_projection_compared_to_null(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2070, 2076,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Last_over_custom_projection_compared_to_not_null(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Last_over_custom_projection_compared_to_not_null(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2078, 2092,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Contains_and_comparison(bool async) => Fixture.NoSyncTest(async, base.Where_Contains_and_comparison);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2094, 2108,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Contains_or_comparison(bool async) => Fixture.NoSyncTest(async, base.Where_Contains_or_comparison);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2110, 2121,
        Justification = Upstream.GaveNoReason)]
    public override Task GetType_on_non_hierarchy1(bool async) => Fixture.NoSyncTest(async, base.GetType_on_non_hierarchy1);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2123, 2135,
        Justification = Upstream.GaveNoReason)]
    public override Task GetType_on_non_hierarchy2(bool async) => Fixture.NoSyncTest(async, base.GetType_on_non_hierarchy2);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2137, 2149,
        Justification = Upstream.GaveNoReason)]
    public override Task GetType_on_non_hierarchy3(bool async) => Fixture.NoSyncTest(async, base.GetType_on_non_hierarchy3);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2151, 2161,
        Justification = Upstream.GaveNoReason)]
    public override Task GetType_on_non_hierarchy4(bool async) => Fixture.NoSyncTest(async, base.GetType_on_non_hierarchy4);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2163, 2174,
        Justification = Upstream.GaveNoReason)]
    public override Task Case_block_simplification_works_correctly(bool async) => Fixture.NoSyncTest(async, base.Case_block_simplification_works_correctly);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2176, 2188,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_compare_null_with_cast_to_object(bool async) => Fixture.NoSyncTest(async, base.Where_compare_null_with_cast_to_object);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2190, 2202,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_compare_with_both_cast_to_object(bool async) => Fixture.NoSyncTest(async, base.Where_compare_with_both_cast_to_object);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2204, 2216,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_projection(bool async) => Fixture.NoSyncTest(async, base.Where_projection);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2218, 2228,
        Justification = Upstream.GaveNoReason)]
    public override Task Enclosing_class_settable_member_generates_parameter(bool async) => Fixture.NoSyncTest(async, base.Enclosing_class_settable_member_generates_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2230, 2237,
        Justification = Upstream.GaveNoReason)]
    public override Task Enclosing_class_readonly_member_generates_parameter(bool async) => Fixture.NoSyncTest(async, base.Enclosing_class_readonly_member_generates_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2239, 2246,
        Justification = Upstream.GaveNoReason)]
    public override Task Enclosing_class_const_member_does_not_generate_parameter(bool async) => Fixture.NoSyncTest(async, base.Enclosing_class_const_member_does_not_generate_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2248, 2261,
        Justification = Upstream.GaveNoReason)]
    public override Task Generic_Ilist_contains_translates_to_server(bool async) => Fixture.NoSyncTest(async, base.Generic_Ilist_contains_translates_to_server);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2263, 2275,
        Justification = Upstream.GaveNoReason)]
    public override Task Multiple_OrElse_on_same_column_converted_to_in_with_overlap(bool async) => Fixture.NoSyncTest(async, base.Multiple_OrElse_on_same_column_converted_to_in_with_overlap);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2277, 2288,
        Justification = Upstream.GaveNoReason)]
    public override Task Multiple_OrElse_on_same_column_with_null_constant_comparison_converted_to_in(bool async) => Fixture.NoSyncTest(async, base.Multiple_OrElse_on_same_column_with_null_constant_comparison_converted_to_in);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2290, 2301,
        Justification = Upstream.GaveNoReason)]
    public override Task Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(bool async) => Fixture.NoSyncTest(async, base.Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2303, 2315,
        Justification = Upstream.GaveNoReason)]
    public override Task Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in_with_overlap(bool async) => Fixture.NoSyncTest(async, base.Constant_array_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in_with_overlap);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2317, 2329,
        Justification = Upstream.GaveNoReason)]
    public override Task Constant_array_Contains_OrElse_another_Contains_gets_combined_to_one_in_with_overlap(bool async) => Fixture.NoSyncTest(async, base.Constant_array_Contains_OrElse_another_Contains_gets_combined_to_one_in_with_overlap);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2331, 2343,
        Justification = Upstream.GaveNoReason)]
    public override Task Constant_array_Contains_AndAlso_another_Contains_gets_combined_to_one_in_with_overlap(bool async) => Fixture.NoSyncTest(async, base.Constant_array_Contains_AndAlso_another_Contains_gets_combined_to_one_in_with_overlap);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2345, 2361,
        Justification = Upstream.GaveNoReason)]
    public override Task Multiple_AndAlso_on_same_column_converted_to_in_using_parameters(bool async) => Fixture.NoSyncTest(async, base.Multiple_AndAlso_on_same_column_converted_to_in_using_parameters);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2363, 2378,
        Justification = Upstream.GaveNoReason)]
    public override Task Array_of_parameters_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in(bool async) => Fixture.NoSyncTest(async, base.Array_of_parameters_Contains_OrElse_comparison_with_constant_gets_combined_to_one_in);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2380, 2394,
        Justification = Upstream.GaveNoReason)]
    public override Task Multiple_OrElse_on_same_column_with_null_parameter_comparison_converted_to_in(bool async) => Fixture.NoSyncTest(async, base.Multiple_OrElse_on_same_column_with_null_parameter_comparison_converted_to_in);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2396, 2410,
        Justification = Upstream.GaveNoReason)]
    public override Task Parameter_array_Contains_OrElse_comparison_with_constant(bool async) => Fixture.NoSyncTest(async, base.Parameter_array_Contains_OrElse_comparison_with_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2412, 2428,
        Justification = Upstream.GaveNoReason)]
    public override Task Parameter_array_Contains_OrElse_comparison_with_parameter_with_overlap(bool async) => Fixture.NoSyncTest(async, base.Parameter_array_Contains_OrElse_comparison_with_parameter_with_overlap);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2430, 2442,
        Justification = Upstream.GaveNoReason)]
    public override Task Two_sets_of_comparison_combine_correctly(bool async) => Fixture.NoSyncTest(async, base.Two_sets_of_comparison_combine_correctly);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2444, 2456,
        Justification = Upstream.GaveNoReason)]
    public override Task Two_sets_of_comparison_combine_correctly2(bool async) => Fixture.NoSyncTest(async, base.Two_sets_of_comparison_combine_correctly2);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2458, 2470,
        Justification = Upstream.GaveNoReason)]
    public override Task Filter_with_property_compared_to_null_wrapped_in_explicit_convert_to_object(bool async) => Fixture.NoSyncTest(async, base.Filter_with_property_compared_to_null_wrapped_in_explicit_convert_to_object);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2486, 2502,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_simple_shadow_projection_mixed(bool async) => Fixture.NoSyncTest(async, base.Where_simple_shadow_projection_mixed);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2504, 2507,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_primitive_tracked(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Where_primitive_tracked(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2509, 2512,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_primitive_tracked2(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Where_primitive_tracked2(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2514, 2535,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_poco_closure(bool async) => Fixture.NoSyncTest(async, base.Where_poco_closure);

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 2537, 2542,
        Justification = "#34327")]
    public override async Task EF_Constant(bool async)
    {
        // #34327
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => base.EF_Constant(async));
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 2544, 2549,
        Justification = "#34327")]
    public override async Task EF_Constant_with_subtree(bool async)
    {
        // #34327
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => base.EF_Constant_with_subtree(async));
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 2551, 2558,
        Justification = "#34327")]
    public override async Task EF_Constant_does_not_parameterized_as_part_of_bigger_subtree(bool async)
    {
        // #34327
        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.EF_Constant_does_not_parameterized_as_part_of_bigger_subtree(async));
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2567, 2574,
        Justification = Upstream.GaveNoReason)]
    public override Task EF_Parameter(bool async) => Fixture.NoSyncTest(async, base.EF_Parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2576, 2583,
        Justification = Upstream.GaveNoReason)]
    public override Task EF_Parameter_with_subtree(bool async) => Fixture.NoSyncTest(async, base.EF_Parameter_with_subtree);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2585, 2599,
        Justification = Upstream.GaveNoReason)]
    public override Task EF_Parameter_does_not_parameterized_as_part_of_bigger_subtree(bool async) => Fixture.NoSyncTest(async, base.EF_Parameter_does_not_parameterized_as_part_of_bigger_subtree);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2608, 2650,
        Justification = Upstream.GaveNoReason)]
    public override Task Implicit_cast_in_predicate(bool async) => Fixture.NoSyncTest(async, base.Implicit_cast_in_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2652, 2684,
        Justification = Upstream.GaveNoReason)]
    public override Task Interface_casting_though_generic_method(bool async) => Fixture.NoSyncTest(async, base.Interface_casting_though_generic_method);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2686, 2696,
        Justification = Upstream.GaveNoReason)]
    public override Task Simplifiable_coalesce_over_nullable(bool async) => Fixture.NoSyncTest(async, base.Simplifiable_coalesce_over_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2698, 2703,
        Justification = Upstream.GaveNoReason)]
    public override Task Take_and_Where_evaluation_order(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Take_and_Where_evaluation_order(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2705, 2708,
        Justification = Upstream.GaveNoReason)]
    public override Task Skip_and_Where_evaluation_order(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Skip_and_Where_evaluation_order(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2710, 2713,
        Justification = Upstream.GaveNoReason)]
    public override Task Take_and_Distinct_evaluation_order(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Take_and_Distinct_evaluation_order(async),
            "The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.");

}
