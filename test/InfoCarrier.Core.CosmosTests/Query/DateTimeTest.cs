// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

namespace InfoCarrier.Core.CosmosTests.Query;

public class DateTimeTest(BasicTypesFixture fixture) : DateTimeTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Temporal/DateTimeTranslationsCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 15, 21,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Now()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Now);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 37, 43,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Today()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Today);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 45, 51,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Date()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Date);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 89, 95,
        Justification = "DateTime.DayOfYear not supported by Cosmos")]
    public override async Task DayOfYear()
    {
        // DateTime.DayOfYear not supported by Cosmos
        await AssertTranslationFailed(base.DayOfYear);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 157, 163,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task TimeOfDay()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.TimeOfDay);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 165, 171,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task subtract_and_TotalDays()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.subtract_and_TotalDays);
    }

}
