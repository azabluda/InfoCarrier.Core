// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace InfoCarrier.Core.CosmosTests.Query;

public class StringTest(BasicTypesFixture fixture) : StringTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/StringTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 259, 260,
        Justification = Upstream.GaveNoReason)]
    public override Task IndexOf_after_ToString()
        => AssertTranslationFailed(base.IndexOf_after_ToString);

    [StoreLimit(UpstreamRepository.EfCore, Source, 262, 263,
        Justification = Upstream.GaveNoReason)]
    public override Task IndexOf_over_ToString()
        => AssertTranslationFailed(base.IndexOf_over_ToString);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 305, 311,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Replace_using_property_arguments()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Replace_using_property_arguments);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 417, 427,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task IsNullOrEmpty()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.IsNullOrEmpty);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 429, 435,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task IsNullOrEmpty_negated()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.IsNullOrEmpty_negated);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 437, 443,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task IsNullOrWhiteSpace()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.IsNullOrWhiteSpace);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 778, 784,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TrimStart_with_char_argument()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.TrimStart_with_char_argument);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 786, 792,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TrimStart_with_char_array_argument()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.TrimStart_with_char_array_argument);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 810, 816,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TrimEnd_with_char_argument()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.TrimEnd_with_char_argument);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 818, 824,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TrimEnd_with_char_array_argument()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.TrimEnd_with_char_array_argument);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 842, 848,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Trim_with_char_argument_in_predicate()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Trim_with_char_argument_in_predicate);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 850, 856,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Trim_with_char_array_argument_in_predicate()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Trim_with_char_array_argument_in_predicate);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 858, 868,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_simple_zero()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_simple_zero);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 870, 876,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_simple_one()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_simple_one);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 878, 887,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_with_parameter()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_with_parameter);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 889, 895,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_simple_more_than_one()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_simple_more_than_one);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 897, 903,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_nested()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_nested);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 905, 911,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_multi_predicate()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_multi_predicate);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 913, 919,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task CompareTo_simple_zero()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.CompareTo_simple_zero);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 921, 927,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task CompareTo_simple_one()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.CompareTo_simple_one);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 929, 938,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task CompareTo_with_parameter()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.CompareTo_with_parameter);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 940, 946,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task CompareTo_simple_more_than_one()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.CompareTo_simple_more_than_one);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 948, 954,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task CompareTo_nested()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.CompareTo_nested);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 956, 962,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Compare_to_multi_predicate()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Compare_to_multi_predicate);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 964, 969,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_over_non_nullable_column()
        => AssertTranslationFailed(base.Join_over_non_nullable_column);

    [StoreLimit(UpstreamRepository.EfCore, Source, 971, 972,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_with_predicate()
        => AssertTranslationFailed(base.Join_with_predicate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 974, 975,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_with_ordering()
        => AssertTranslationFailed(base.Join_with_ordering);

    [StoreLimit(UpstreamRepository.EfCore, Source, 977, 978,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_over_nullable_column()
        => AssertTranslationFailed(base.Join_over_nullable_column);

    [StoreLimit(UpstreamRepository.EfCore, Source, 980, 981,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_non_aggregate()
        => AssertTranslationFailed(base.Join_non_aggregate);

    [StoreLimit(UpstreamRepository.EfCore, Source, 999, 1000,
        Justification = Upstream.GaveNoReason)]
    public override Task Concat_aggregate()
        => AssertTranslationFailed(base.Concat_aggregate);

}
