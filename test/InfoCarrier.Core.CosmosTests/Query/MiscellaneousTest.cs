// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace InfoCarrier.Core.CosmosTests.Query;

public class MiscellaneousTest(BasicTypesFixture fixture) : MiscellaneousTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/MiscellaneousTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 29, 34,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_Shared_Next_with_no_args()
    {
        await AssertTranslationFailed(base.Random_Shared_Next_with_no_args);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 36, 41,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_Shared_Next_with_one_arg()
    {
        await AssertTranslationFailed(base.Random_Shared_Next_with_one_arg);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 43, 48,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_Shared_Next_with_two_args()
    {
        await AssertTranslationFailed(base.Random_Shared_Next_with_two_args);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 50, 55,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_new_Next_with_no_args()
    {
        await AssertTranslationFailed(base.Random_new_Next_with_no_args);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 57, 62,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_new_Next_with_one_arg()
    {
        await AssertTranslationFailed(base.Random_new_Next_with_one_arg);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 64, 69,
        Justification = Upstream.GaveNoReason)]
    public override async Task Random_new_Next_with_two_args()
    {
        await AssertTranslationFailed(base.Random_new_Next_with_two_args);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 71, 81,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToBoolean()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToBoolean);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 83, 89,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToByte()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToByte);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 91, 97,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToDecimal()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToDecimal);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 99, 105,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToDouble()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToDouble);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 107, 113,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToInt16()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToInt16);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 115, 121,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToInt32()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToInt32);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 123, 129,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToInt64()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToInt64);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 131, 137,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Convert_ToString()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Convert_ToString);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 139, 149,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Byte_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Byte_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 151, 157,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Decimal_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Decimal_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 159, 165,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Double_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Double_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 167, 173,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Short_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Short_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 175, 181,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Int_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Int_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 183, 189,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Long_Parse()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Long_Parse);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 191, 201,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Int_Compare_to_simple_zero()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Int_Compare_to_simple_zero);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 203, 209,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DateTime_Compare_to_simple_zero(bool compareTo)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.DateTime_Compare_to_simple_zero(compareTo));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 211, 217,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TimeSpan_Compare_to_simple_zero(bool compareTo)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.TimeSpan_Compare_to_simple_zero(compareTo));
    }

}
