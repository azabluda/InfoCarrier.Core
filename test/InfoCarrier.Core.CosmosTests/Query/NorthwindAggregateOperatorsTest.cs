// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindAggregateOperatorsTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindAggregateOperatorsQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindAggregateOperatorsQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 26, 38,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_over_default_returns_default(bool async) => Fixture.NoSyncTest(async, base.Average_over_default_returns_default);

    [StoreIssue(IssueTracker.EfCore, 33931, UpstreamRepository.EfCore, Source, 40, 50,
        Justification = "TODO: #33931",
        Deviation = DeviationKind.Other,
        DeviationNote = "Pass the inherited execution mode; upstream forces synchronous mode for both data cases.")]
    public override Task Contains_over_keyless_entity_throws(bool async)
        => Fixture.NoSyncTest(false, _ => base.Contains_over_keyless_entity_throws(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 52, 66,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_non_primitive_list_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_non_primitive_list_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 68, 90,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_non_primitive_list_inline_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_non_primitive_list_inline_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 92, 116,
        Justification = Upstream.GaveNoReason)]
    public override Task Count_on_projection_with_client_eval(bool async) => Fixture.NoSyncTest(async, base.Count_on_projection_with_client_eval);

    [StoreLimit(UpstreamRepository.EfCore, Source, 118, 131,
        Justification = Upstream.GaveNoReason)]
    public override Task First(bool async) => Fixture.NoSyncTest(async, base.First);

    [StoreLimit(UpstreamRepository.EfCore, Source, 133, 145,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_over_default_returns_default(bool async) => Fixture.NoSyncTest(async, base.Max_over_default_returns_default);

    [StoreLimit(UpstreamRepository.EfCore, Source, 147, 159,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_over_default_returns_default(bool async) => Fixture.NoSyncTest(async, base.Min_over_default_returns_default);

    [StoreLimit(UpstreamRepository.EfCore, Source, 161, 173,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_over_empty_returns_zero(bool async) => Fixture.NoSyncTest(async, base.Sum_over_empty_returns_zero);

    [StoreLimit(UpstreamRepository.EfCore, Source, 175, 189,
        Justification = Upstream.GaveNoReason)]
    public override Task First_Predicate(bool async) => Fixture.NoSyncTest(async, base.First_Predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 191, 205,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 191, 205,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Single_Throws(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.Single_Throws(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 207, 221,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_First(bool async) => Fixture.NoSyncTest(async, base.Where_First);

    [StoreLimit(UpstreamRepository.EfCore, Source, 223, 230,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Single(bool async) => Fixture.NoSyncTest(async, base.Where_Single);

    [StoreLimit(UpstreamRepository.EfCore, Source, 232, 245,
        Justification = Upstream.GaveNoReason)]
    public override Task FirstOrDefault(bool async) => Fixture.NoSyncTest(async, base.FirstOrDefault);

    [StoreLimit(UpstreamRepository.EfCore, Source, 247, 259,
        Justification = Upstream.GaveNoReason)]
    public override Task Array_cast_to_IEnumerable_Contains_with_constant(bool async) => Fixture.NoSyncTest(async, base.Array_cast_to_IEnumerable_Contains_with_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 261, 275,
        Justification = Upstream.GaveNoReason)]
    public override Task FirstOrDefault_Predicate(bool async) => Fixture.NoSyncTest(async, base.FirstOrDefault_Predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 277, 284,
        Justification = Upstream.GaveNoReason)]
    public override Task SingleOrDefault_Predicate(bool async) => Fixture.NoSyncTest(async, base.SingleOrDefault_Predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 286, 300,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 286, 300,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task SingleOrDefault_Throws(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.SingleOrDefault_Throws(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 302, 316,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_FirstOrDefault(bool async) => Fixture.NoSyncTest(async, base.Where_FirstOrDefault);

    [StoreLimit(UpstreamRepository.EfCore, Source, 318, 325,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_SingleOrDefault(bool async) => Fixture.NoSyncTest(async, base.Where_SingleOrDefault);

    [StoreLimit(UpstreamRepository.EfCore, Source, 327, 337,
        Case = "async: False", Skip = true, Justification = "Always throws sync-not-support for sync.")]
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 327, 337,
        Case = "async: True", Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Select_All(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws sync-not-support for sync.");
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Select_All(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 339, 351,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_no_arg(bool async) => Fixture.NoSyncTest(async, base.Sum_with_no_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 353, 365,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_no_data_cast_to_nullable(bool async) => Fixture.NoSyncTest(async, base.Sum_with_no_data_cast_to_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 367, 379,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_binary_expression(bool async) => Fixture.NoSyncTest(async, base.Sum_with_binary_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 381, 393,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_no_arg_empty(bool async) => Fixture.NoSyncTest(async, base.Sum_with_no_arg_empty);

    [StoreLimit(UpstreamRepository.EfCore, Source, 395, 407,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_no_data_nullable(bool async) => Fixture.NoSyncTest(async, base.Sum_with_no_data_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 409, 421,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_arg(bool async) => Fixture.NoSyncTest(async, base.Sum_with_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 423, 435,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_arg_expression(bool async) => Fixture.NoSyncTest(async, base.Sum_with_arg_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 437, 449,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_division_on_decimal(bool async) => Fixture.NoSyncTest(async, base.Sum_with_division_on_decimal);

    [StoreLimit(UpstreamRepository.EfCore, Source, 451, 463,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_division_on_decimal_no_significant_digits(bool async) => Fixture.NoSyncTest(async, base.Sum_with_division_on_decimal_no_significant_digits);

    [StoreLimit(UpstreamRepository.EfCore, Source, 465, 477,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_with_coalesce(bool async) => Fixture.NoSyncTest(async, base.Sum_with_coalesce);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 479, 485,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 487, 493,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_nested_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_nested_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 495, 501,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_min_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_min_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 503, 509,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_scalar_returning_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_scalar_returning_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 511, 517,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_Any_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_Any_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 519, 525,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_over_uncorrelated_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_over_uncorrelated_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 527, 539,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_on_float_column(bool async) => Fixture.NoSyncTest(async, base.Sum_on_float_column);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 541, 547,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Sum_on_float_column_in_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Sum_on_float_column_in_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 549, 563,
        Case = "async: False", Skip = true, Justification = "Sync always throws before getting to exception being tested.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 549, 563,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Average_no_data(bool async)
    {
        if (!async) throw SkipException.ForSkip("Sync always throws before getting to exception being tested.");
        await base.Average_no_data(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 565, 577,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_no_data_nullable(bool async) => Fixture.NoSyncTest(async, base.Average_no_data_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 579, 591,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_no_data_cast_to_nullable(bool async) => Fixture.NoSyncTest(async, base.Average_no_data_cast_to_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 593, 607,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 593, 607,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Min_no_data(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.Min_no_data(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 609, 623,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 609, 623,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task Max_no_data(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.Max_no_data(async);
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 625, 631,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_no_data_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_no_data_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 633, 639,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Max_no_data_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Max_no_data_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 641, 653,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_no_data_nullable(bool async) => Fixture.NoSyncTest(async, base.Max_no_data_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 655, 667,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_no_data_cast_to_nullable(bool async) => Fixture.NoSyncTest(async, base.Max_no_data_cast_to_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 669, 685,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 669, 685,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_no_data_value_type(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MaxBy_no_data_value_type(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 687, 703,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 687, 703,
        Case = "async: True", Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type.")]
    public override async Task MaxBy_no_data_nullable_source(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.MaxBy_no_data_nullable_source(async));
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 705, 721,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 705, 721,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_no_data_reference_type_source(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MaxBy_no_data_reference_type_source(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 723, 739,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 723, 739,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_no_data_nullable_selector(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MaxBy_no_data_nullable_selector(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 741, 746,
        Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_no_data_subquery_reference_type(bool async)
    {
        await AssertTranslationFailed(() => base.MaxBy_no_data_subquery_reference_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 748, 753,
        Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_no_data_subquery_value_type(bool async)
    {
        await AssertTranslationFailed(() => base.MaxBy_no_data_subquery_value_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 755, 769,
        Justification = Upstream.GaveNoReason)]
    public override Task MaxBy(bool async) => Fixture.NoSyncTest(async, base.MaxBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 771, 787,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 771, 787,
        Case = "async: True", Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type.")]
    public override async Task MaxBy_with_coalesce(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.MaxBy_with_coalesce(async));
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 789, 795,
        Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_over_subquery(bool async)
    {
        //Cosmos subqueries must be correlated, referencing values from the outer query
        await AssertTranslationFailed(() => base.MaxBy_over_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 797, 803,
        Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_over_nested_subquery(bool async)
    {
        // The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.
        await AssertTranslationFailed(() => base.MaxBy_over_nested_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 805, 811,
        Justification = Upstream.GaveNoReason)]
    public override async Task MaxBy_over_sum_subquery(bool async)
    {
        // The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos
        await AssertTranslationFailed(() => base.MaxBy_over_sum_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 813, 819,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Min_no_data_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Min_no_data_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 821, 835,
        Justification = Upstream.GaveNoReason)]
    public override Task MinBy(bool async) => Fixture.NoSyncTest(async, base.MinBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 837, 853,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 837, 853,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_no_data_value_type(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MinBy_no_data_value_type(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 855, 871,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 855, 871,
        Case = "async: True", Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type.")]
    public override async Task MinBy_no_data_nullable_source(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.MinBy_no_data_nullable_source(async));
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 873, 889,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 873, 889,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_no_data_reference_type_source(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MinBy_no_data_reference_type_source(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 891, 907,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 891, 907,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_no_data_nullable_selector(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        await base.MinBy_no_data_nullable_selector(async);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 909, 914,
        Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_no_data_subquery_reference_type(bool async)
    {
        await AssertTranslationFailed(() => base.MinBy_no_data_subquery_reference_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 916, 921,
        Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_no_data_subquery_value_type(bool async)
    {
        await AssertTranslationFailed(() => base.MinBy_no_data_subquery_value_type(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 923, 939,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 923, 939,
        Case = "async: True", Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type.")]
    public override async Task MinBy_with_coalesce(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.MinBy_with_coalesce(async));
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 941, 947,
        Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_over_subquery(bool async)
    {
        // Cosmos subqueries must be correlated, referencing values from the outer query.
        await AssertTranslationFailed(() => base.MinBy_over_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 949, 955,
        Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_over_nested_subquery(bool async)
    {
        // The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.
        await AssertTranslationFailed(() => base.MinBy_over_nested_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 957, 963,
        Justification = Upstream.GaveNoReason)]
    public override async Task MinBy_over_max_subquery(bool async)
    {
        // The query requires use of LIMIT and OFFSET in a subquery, which is unsupported by Cosmos.
        await AssertTranslationFailed(() => base.MinBy_over_max_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 965, 977,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_no_arg(bool async) => Fixture.NoSyncTest(async, base.Average_with_no_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 979, 991,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_binary_expression(bool async) => Fixture.NoSyncTest(async, base.Average_with_binary_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 993, 1005,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_arg(bool async) => Fixture.NoSyncTest(async, base.Average_with_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1007, 1019,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_arg_expression(bool async) => Fixture.NoSyncTest(async, base.Average_with_arg_expression);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1021, 1033,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_division_on_decimal(bool async) => Fixture.NoSyncTest(async, base.Average_with_division_on_decimal);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1035, 1047,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_division_on_decimal_no_significant_digits(bool async) => Fixture.NoSyncTest(async, base.Average_with_division_on_decimal_no_significant_digits);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1049, 1061,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_coalesce(bool async) => Fixture.NoSyncTest(async, base.Average_with_coalesce);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1063, 1069,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_over_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_over_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1071, 1077,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_over_nested_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_over_nested_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1079, 1085,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_over_max_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_over_max_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1087, 1099,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_on_float_column(bool async) => Fixture.NoSyncTest(async, base.Average_on_float_column);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1101, 1107,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_on_float_column_in_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_on_float_column_in_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1109, 1115,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_on_float_column_in_subquery_with_cast(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_on_float_column_in_subquery_with_cast(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1117, 1129,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_with_no_arg(bool async) => Fixture.NoSyncTest(async, base.Min_with_no_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1131, 1143,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_with_arg(bool async) => Fixture.NoSyncTest(async, base.Min_with_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1145, 1157,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_no_data_nullable(bool async) => Fixture.NoSyncTest(async, base.Min_no_data_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1159, 1171,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_no_data_cast_to_nullable(bool async) => Fixture.NoSyncTest(async, base.Min_no_data_cast_to_nullable);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1173, 1185,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_with_coalesce(bool async) => Fixture.NoSyncTest(async, base.Min_with_coalesce);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1187, 1193,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Min_over_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Min_over_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1195, 1201,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Min_over_nested_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Min_over_nested_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1203, 1209,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Min_over_max_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Min_over_max_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1211, 1223,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_with_no_arg(bool async) => Fixture.NoSyncTest(async, base.Max_with_no_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1225, 1237,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_with_arg(bool async) => Fixture.NoSyncTest(async, base.Max_with_arg);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1239, 1251,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_with_coalesce(bool async) => Fixture.NoSyncTest(async, base.Max_with_coalesce);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1253, 1259,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Max_over_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Max_over_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1261, 1267,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Max_over_nested_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Max_over_nested_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1269, 1275,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Max_over_sum_subquery(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Max_over_sum_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1277, 1289,
        Justification = Upstream.GaveNoReason)]
    public override Task Count_with_no_predicate(bool async) => Fixture.NoSyncTest(async, base.Count_with_no_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1291, 1303,
        Justification = Upstream.GaveNoReason)]
    public override Task Count_with_predicate(bool async) => Fixture.NoSyncTest(async, base.Count_with_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1305, 1317,
        Justification = Upstream.GaveNoReason)]
    public override Task Count_with_order_by(bool async) => Fixture.NoSyncTest(async, base.Count_with_order_by);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1319, 1331,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_OrderBy_Count(bool async) => Fixture.NoSyncTest(async, base.Where_OrderBy_Count);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1333, 1345,
        Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_Where_Count(bool async) => Fixture.NoSyncTest(async, base.OrderBy_Where_Count);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1347, 1359,
        Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_Count_with_predicate(bool async) => Fixture.NoSyncTest(async, base.OrderBy_Count_with_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1361, 1373,
        Justification = Upstream.GaveNoReason)]
    public override Task OrderBy_Where_Count_with_predicate(bool async) => Fixture.NoSyncTest(async, base.OrderBy_Where_Count_with_predicate);

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1424, 1430,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Average_on_nav_subquery_in_projection(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Average_on_nav_subquery_in_projection(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1432, 1438,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Count_after_client_projection(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Count_after_client_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1440, 1457,
        Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 1440, 1457,
        Case = "async: True", Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type.")]
    public override async Task OrderBy_client_Take(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws for sync.");
        var error = await Assert.ThrowsAsync<InfoCarrierServerException>(async () => await base.OrderBy_client_Take(async));
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1459, 1470,
        Justification = Upstream.GaveNoReason)]
    public override Task Distinct(bool async) => Fixture.NoSyncTest(async, base.Distinct);

    [StoreIssue(IssueTracker.EfCore, 27688, UpstreamRepository.EfCore, Source, 1472, 1485,
        Skip = true, Justification = "Test does not run on CI",
        Deviation = DeviationKind.Other,
        DeviationNote = "Use the existing GitHub Actions CI flag for the upstream conditional CI exclusion.")]
    public override Task Distinct_Scalar(bool async)
        => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            ? throw SkipException.ForSkip("Test does not run on CI")
            : Fixture.NoSyncTest(async, base.Distinct_Scalar);

    [StoreDefect("2.6", UpstreamRepository.EfCore, Source, 1487, 1501,
        Skip = true, Justification = "Fails on emulator https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4339")]
    public override Task OrderBy_Distinct(bool async) => throw SkipException.ForSkip("Fails on emulator https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4339");

    [StoreIssue(IssueTracker.EfCore, 16156, UpstreamRepository.EfCore, Source, 1503, 1507,
        Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_OrderBy(bool async)
        // Subquery pushdown. Issue #16156.
        => await AssertTranslationFailedWithDetails(
            () => base.Distinct_OrderBy(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");

    [StoreIssue(IssueTracker.EfCore, 16156, UpstreamRepository.EfCore, Source, 1509, 1513,
        Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_OrderBy2(bool async)
        // Subquery pushdown. Issue #16156.
        => await AssertTranslationFailedWithDetails(
            () => base.Distinct_OrderBy2(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");

    [StoreIssue(IssueTracker.EfCore, 16156, UpstreamRepository.EfCore, Source, 1515, 1523,
        Justification = "Subquery pushdown. Issue #16156.")]
    public override async Task Distinct_OrderBy3(bool async)
    {
        // Subquery pushdown. Issue #16156.
        await AssertTranslationFailedWithDetails(
            () => base.Distinct_OrderBy(async),
            "Azure Cosmos DB does not have an appropriate subquery for this translation.");
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1525, 1531,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Distinct_Count(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Distinct_Count(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 1533, 1539,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Select_Select_Distinct_Count(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Select_Select_Distinct_Count(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1541, 1548,
        Justification = Upstream.GaveNoReason)]
    public override Task Single_Predicate(bool async) => Fixture.NoSyncTest(async, base.Single_Predicate);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1550, 1556,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task FirstOrDefault_inside_subquery_gets_server_evaluated(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.FirstOrDefault_inside_subquery_gets_server_evaluated(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1558, 1564,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Multiple_collection_navigation_with_FirstOrDefault_chained_projecting_scalar(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Multiple_collection_navigation_with_FirstOrDefault_chained_projecting_scalar(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1566, 1572,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task First_inside_subquery_gets_client_evaluated(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.First_inside_subquery_gets_client_evaluated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1574, 1587,
        Justification = Upstream.GaveNoReason)]
    public override Task Last(bool async) => Fixture.NoSyncTest(async, base.Last);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1589, 1596,
        Justification = Upstream.GaveNoReason)]
    public override Task Last_when_no_order_by(bool async) => Fixture.NoSyncTest(async, base.Last_when_no_order_by);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1598, 1605,
        Justification = Upstream.GaveNoReason)]
    public override Task LastOrDefault_when_no_order_by(bool async) => Fixture.NoSyncTest(async, base.LastOrDefault_when_no_order_by);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1607, 1621,
        Justification = Upstream.GaveNoReason)]
    public override Task Last_Predicate(bool async) => Fixture.NoSyncTest(async, base.Last_Predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1623, 1637,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_Last(bool async) => Fixture.NoSyncTest(async, base.Where_Last);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1639, 1652,
        Justification = Upstream.GaveNoReason)]
    public override Task LastOrDefault(bool async) => Fixture.NoSyncTest(async, base.LastOrDefault);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1654, 1668,
        Justification = Upstream.GaveNoReason)]
    public override Task LastOrDefault_Predicate(bool async) => Fixture.NoSyncTest(async, base.LastOrDefault_Predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1670, 1684,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_LastOrDefault(bool async) => Fixture.NoSyncTest(async, base.Where_LastOrDefault);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1686, 1692,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_with_subquery(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_subquery(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1694, 1716,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_array_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_array_closure);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 1718, 1724,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_with_subquery_and_local_array_closure(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_subquery_and_local_array_closure(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1726, 1748,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_uint_array_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_uint_array_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1750, 1772,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_nullable_uint_array_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_nullable_uint_array_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1774, 1786,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_array_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_array_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1788, 1802,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_list_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_list_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1804, 1818,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_object_list_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_object_list_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1820, 1834,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_list_closure_all_null(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_list_closure_all_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1836, 1848,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_list_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_list_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1850, 1872,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_list_inline_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_list_inline_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1874, 1895,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_enumerable_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_enumerable_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1897, 1911,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_object_enumerable_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_object_enumerable_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1913, 1927,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_enumerable_closure_all_null(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_enumerable_closure_all_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1929, 1944,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_enumerable_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_enumerable_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1946, 1974,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_enumerable_inline_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_enumerable_inline_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1976, 1998,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_ordered_enumerable_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_ordered_enumerable_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2000, 2014,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_object_ordered_enumerable_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_object_ordered_enumerable_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2016, 2030,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_ordered_enumerable_closure_all_null(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_ordered_enumerable_closure_all_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2032, 2044,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_ordered_enumerable_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_ordered_enumerable_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2046, 2068,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_ordered_enumerable_inline_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_ordered_enumerable_inline_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2070, 2092,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_read_only_collection_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_read_only_collection_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2094, 2108,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_object_read_only_collection_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_object_read_only_collection_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2110, 2124,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_ordered_read_only_collection_all_null(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_ordered_read_only_collection_all_null);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2126, 2138,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_read_only_collection_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_read_only_collection_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2140, 2162,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_read_only_collection_inline_closure_mix(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_read_only_collection_inline_closure_mix);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2164, 2178,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_false(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_false);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2180, 2194,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_complex_predicate_and(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_complex_predicate_and);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2196, 2210,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_complex_predicate_or(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_complex_predicate_or);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2212, 2226,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_complex_predicate_not_matching_ins1(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_complex_predicate_not_matching_ins1);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2228, 2242,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_complex_predicate_not_matching_ins2(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_complex_predicate_not_matching_ins2);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2244, 2258,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_sql_injection(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_sql_injection);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2260, 2274,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_empty_closure(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_empty_closure);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2276, 2288,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_local_collection_empty_inline(bool async) => Fixture.NoSyncTest(async, base.Contains_with_local_collection_empty_inline);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2290, 2305,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_top_level(bool async) => Fixture.NoSyncTest(async, base.Contains_top_level);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2307, 2313,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_with_local_tuple_array_closure(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_local_tuple_array_closure(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2315, 2321,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_with_local_anonymous_type_array_closure(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_with_local_anonymous_type_array_closure(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2323, 2329,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task OfType_Select(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.OfType_Select(async));
    }

    [StoreIssue(IssueTracker.EfCore, 15937, UpstreamRepository.EfCore, Source, 2331, 2337,
        Justification = "Contains over subquery. Issue #15937.")]
    public override async Task OfType_Select_OfType_Select(bool async)
    {
        // Contains over subquery. Issue #15937.
        await AssertTranslationFailed(() => base.OfType_Select_OfType_Select(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2339, 2351,
        Justification = Upstream.GaveNoReason)]
    public override Task Average_with_non_matching_types_in_projection_doesnt_produce_second_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Average_with_non_matching_types_in_projection_doesnt_produce_second_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2353, 2365,
        Justification = Upstream.GaveNoReason)]
    public override Task Max_with_non_matching_types_in_projection_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Max_with_non_matching_types_in_projection_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2367, 2379,
        Justification = Upstream.GaveNoReason)]
    public override Task Min_with_non_matching_types_in_projection_introduces_explicit_cast(bool async) => Fixture.NoSyncTest(async, base.Min_with_non_matching_types_in_projection_introduces_explicit_cast);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2381, 2393,
        Case = "async: False", Skip = true, Justification = "Always throws sync-not-support for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 2381, 2393,
        Case = "async: True", Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_Take_Last_gives_correct_result(bool async)
    {
        if (!async) throw SkipException.ForSkip("Always throws sync-not-support for sync.");
        Assert.Equal(
                "Reversing the ordering is not supported when limit or offset are already applied.",
                (await Assert.ThrowsAsync<InvalidOperationException>(async () => await base.OrderBy_Take_Last_gives_correct_result(async)))
                .Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2395, 2403,
        Justification = Upstream.GaveNoReason)]
    public override async Task OrderBy_Skip_Last_gives_correct_result(bool async)
    {
        Assert.Equal(
            "Reversing the ordering is not supported when limit or offset are already applied.",
            (await Assert.ThrowsAsync<InvalidOperationException>(async () => await base.OrderBy_Skip_Last_gives_correct_result(async)))
            .Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2405, 2413,
        Justification = "Inner query is always sync.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Exercise both execution modes; upstream leaves its asynchronous arm empty.")]
    public override Task Contains_over_entityType_should_rewrite_to_identity_equality(bool async)
        => Fixture.NoSyncTest(false, _ => base.Contains_over_entityType_should_rewrite_to_identity_equality(async));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2415, 2421,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task List_Contains_over_entityType_should_rewrite_to_identity_equality(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.List_Contains_over_entityType_should_rewrite_to_identity_equality(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2423, 2435,
        Justification = Upstream.GaveNoReason)]
    public override Task List_Contains_with_constant_list(bool async) => Fixture.NoSyncTest(async, base.List_Contains_with_constant_list);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2437, 2449,
        Justification = Upstream.GaveNoReason)]
    public override Task List_Contains_with_parameter_list(bool async) => Fixture.NoSyncTest(async, base.List_Contains_with_parameter_list);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2451, 2463,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_parameter_list_value_type_id(bool async) => Fixture.NoSyncTest(async, base.Contains_with_parameter_list_value_type_id);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2465, 2477,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_constant_list_value_type_id(bool async) => Fixture.NoSyncTest(async, base.Contains_with_constant_list_value_type_id);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2479, 2493,
        Justification = Upstream.GaveNoReason)]
    public override Task IImmutableSet_Contains_with_parameter(bool async) => Fixture.NoSyncTest(async, base.IImmutableSet_Contains_with_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2495, 2509,
        Justification = Upstream.GaveNoReason)]
    public override Task IReadOnlySet_Contains_with_parameter(bool async) => Fixture.NoSyncTest(async, base.IReadOnlySet_Contains_with_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2511, 2525,
        Justification = Upstream.GaveNoReason)]
    public override Task HashSet_Contains_with_parameter(bool async) => Fixture.NoSyncTest(async, base.HashSet_Contains_with_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2527, 2541,
        Justification = Upstream.GaveNoReason)]
    public override Task ImmutableHashSet_Contains_with_parameter(bool async) => Fixture.NoSyncTest(async, base.ImmutableHashSet_Contains_with_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2543, 2558,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_entityType_with_null_should_rewrite_to_false(bool async) => Fixture.NoSyncTest(async, base.Contains_over_entityType_with_null_should_rewrite_to_false);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2560, 2566,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_with_null_in_projection(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_entityType_with_null_in_projection(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2568, 2579,
        Justification = Upstream.GaveNoReason)]
    public override Task String_FirstOrDefault_in_projection_does_not_do_client_eval(bool async) => Fixture.NoSyncTest(async, base.String_FirstOrDefault_in_projection_does_not_do_client_eval);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2581, 2592,
        Justification = Upstream.GaveNoReason)]
    public override Task Project_constant_Sum(bool async) => Fixture.NoSyncTest(async, base.Project_constant_Sum);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2594, 2608,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_any_equals_operator(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_any_equals_operator);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2610, 2622,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_any_equals(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_any_equals);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2624, 2638,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_any_equals_static(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_any_equals_static);

    [StoreDefect("2.5", UpstreamRepository.EfCore, Source, 2640, 2665,
        Skip = true, Justification = "EXISTS/ANY/ALL subqueries cause internal server error")]
    public override Task Where_subquery_where_any(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2667, 2681,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_all_not_equals_operator(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_all_not_equals_operator);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2683, 2695,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_all_not_equals(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_all_not_equals);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2697, 2711,
        Justification = Upstream.GaveNoReason)]
    public override Task Where_subquery_all_not_equals_static(bool async) => Fixture.NoSyncTest(async, base.Where_subquery_all_not_equals_static);

    [StoreDefect("2.5", UpstreamRepository.EfCore, Source, 2713, 2738,
        Skip = true, Justification = "EXISTS/ANY/ALL subqueries cause internal server error")]
    public override Task Where_subquery_where_all(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 2740, 2751,
        Justification = Upstream.GaveNoReason)]
    public override Task Cast_to_same_Type_Count_works(bool async) => Fixture.NoSyncTest(async, base.Cast_to_same_Type_Count_works);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2753, 2759,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Cast_before_aggregate_is_preserved(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Cast_before_aggregate_is_preserved(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2761, 2767,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Enumerable_min_is_mapped_to_Queryable_1(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Enumerable_min_is_mapped_to_Queryable_1(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2769, 2775,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Enumerable_min_is_mapped_to_Queryable_2(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Enumerable_min_is_mapped_to_Queryable_2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2777, 2783,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task DefaultIfEmpty_selects_only_required_columns(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.DefaultIfEmpty_selects_only_required_columns(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2785, 2791,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Collection_Last_member_access_in_projection_translated(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_Last_member_access_in_projection_translated(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2793, 2799,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Collection_LastOrDefault_member_access_in_projection_translated(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_LastOrDefault_member_access_in_projection_translated(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2801, 2813,
        Justification = Upstream.GaveNoReason)]
    public override Task Sum_over_explicit_cast_over_column(bool async) => Fixture.NoSyncTest(async, base.Sum_over_explicit_cast_over_column);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2815, 2821,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_scalar_with_null_should_rewrite_to_identity_equality_subquery(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_scalar_with_null_should_rewrite_to_identity_equality_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2823, 2829,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_nullable_scalar_with_null_in_subquery_translated_correctly(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_nullable_scalar_with_null_in_subquery_translated_correctly(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2831, 2837,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_non_nullable_scalar_with_null_in_subquery_simplifies_to_false(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_non_nullable_scalar_with_null_in_subquery_simplifies_to_false(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2839, 2845,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2847, 2854,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_complex(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(()
            => base.Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_complex(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2856, 2863,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_negated(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(()
            => base.Contains_over_entityType_with_null_should_rewrite_to_identity_equality_subquery_negated(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2865, 2871,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_should_materialize_when_composite(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_entityType_should_materialize_when_composite(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2873, 2879,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Contains_over_entityType_should_materialize_when_composite2(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Contains_over_entityType_should_materialize_when_composite2(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2881, 2887,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Average_after_DefaultIfEmpty_does_not_throw(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Average_after_DefaultIfEmpty_does_not_throw(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2889, 2895,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Max_after_DefaultIfEmpty_does_not_throw(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Max_after_DefaultIfEmpty_does_not_throw(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2897, 2903,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task MaxBy_after_DefaultIfEmpty_does_not_throw(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.MaxBy_after_DefaultIfEmpty_does_not_throw(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2905, 2911,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Min_after_DefaultIfEmpty_does_not_throw(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Min_after_DefaultIfEmpty_does_not_throw(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2913, 2919,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task MinBy_after_DefaultIfEmpty_does_not_throw(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.MinBy_after_DefaultIfEmpty_does_not_throw(async));
    }

    [StoreIssue(IssueTracker.EfCore, 20677, UpstreamRepository.EfCore, Source, 2921, 2926,
        Justification = "Aggregate selecting non-mapped type. Issue #20677.")]
    public override Task Average_with_unmapped_property_access_throws_meaningful_exception(bool async)
        // Aggregate selecting non-mapped type. Issue #20677.
        => AssertTranslationFailed(() => AssertAverage(
            async,
            ss => ss.Set<Order>(),
            selector: c => c.ShipVia));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 2928, 2934,
        Justification = "Contains over subquery. Issue #17246.")]
    public override async Task Multiple_collection_navigation_with_FirstOrDefault_chained(bool async)
    {
        // Contains over subquery. Issue #17246.
        await AssertTranslationFailed(() => base.Multiple_collection_navigation_with_FirstOrDefault_chained(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 2936, 2942,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task All_true(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.All_true(async));
    }

    [StoreIssue(IssueTracker.EfCore, 16146, UpstreamRepository.EfCore, Source, 2944, 2950,
        Justification = "Aggregates. Issue #16146.")]
    public override async Task Not_Any_false(bool async)
    {
        // Aggregates. Issue #16146.
        await AssertTranslationFailed(() => base.Not_Any_false(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 2952, 2958,
        Justification = "GroupBy. Issue #17313.")]
    public override async Task Contains_inside_aggregate_function_with_GroupBy(bool async)
    {
        // GroupBy. Issue #17313.
        await AssertTranslationFailed(() => base.Contains_inside_aggregate_function_with_GroupBy(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2960, 2973,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_Average_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_Average_without_GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2975, 2988,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_Sum_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_Sum_without_GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2990, 3004,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_Count_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_Count_without_GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 3006, 3020,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_LongCount_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_LongCount_without_GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 3022, 3035,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_Max_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_Max_without_GroupBy);

    [StoreLimit(UpstreamRepository.EfCore, Source, 3037, 3050,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_inside_Min_without_GroupBy(bool async) => Fixture.NoSyncTest(async, base.Contains_inside_Min_without_GroupBy);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 3052, 3107,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Return_type_of_singular_operator_is_preserved(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3109, 3121,
        Justification = Upstream.GaveNoReason)]
    public override Task Type_casting_inside_sum(bool async) => Fixture.NoSyncTest(async, base.Type_casting_inside_sum);

}
