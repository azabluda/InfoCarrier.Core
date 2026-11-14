// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class DateTimeOffsetTest(BasicTypesFixture fixture) : DateTimeOffsetTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 17, 23,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Now()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Now);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 37, 43,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Date()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Date);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 59, 72,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310",
        Deviation = DeviationKind.Other,
        DeviationNote = "The tier always uses the emulator, so the upstream non-emulator guard is inapplicable.")]
    public override async Task Month()
    {

        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.Month);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 74, 80,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DayOfYear()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.DayOfYear);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 96, 109,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310",
        Deviation = DeviationKind.Other,
        DeviationNote = "The tier always uses the emulator, so the upstream non-emulator guard is inapplicable.")]
    public override async Task Hour()
    {

        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.Hour);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 111, 124,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310",
        Deviation = DeviationKind.Other,
        DeviationNote = "The tier always uses the emulator, so the upstream non-emulator guard is inapplicable.")]
    public override async Task Minute()
    {

        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.Minute);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 168, 179,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task Nanosecond()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.Nanosecond);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 192, 198,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task DateTime()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.DateTime);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 200, 206,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task UtcDateTime()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.UtcDateTime);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 208, 214,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task LocalDateTime()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.LocalDateTime);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 216, 226,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddYears()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddYears);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 228, 238,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddMonths()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddMonths);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 240, 250,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call AddDays; upstream calls AddSeconds.")]
    public override async Task AddDays()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddDays);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 252, 262,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddHours()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddHours);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 264, 274,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddMinutes()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddMinutes);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 276, 286,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddSeconds()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddSeconds);
    }

    [StoreIssue(IssueTracker.EfCore, 35310, UpstreamRepository.EfCore, Source, 288, 298,
        Justification = "Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310")]
    public override async Task AddMilliseconds()
    {
        // Our persisted representation of DateTimeOffset (xxx+00:00) isn't supported by Cosmos (should be xxxZ). #35310
        await Assert.ThrowsAsync<EqualException>(base.AddMilliseconds);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 300, 301,
        Justification = Upstream.GaveNoReason)]
    public override Task ToUnixTimeMilliseconds()
        => AssertTranslationFailed(base.ToUnixTimeMilliseconds);

    [StoreLimit(UpstreamRepository.EfCore, Source, 303, 304,
        Justification = Upstream.GaveNoReason)]
    public override Task ToUnixTimeSecond()
        => AssertTranslationFailed(base.ToUnixTimeSecond);

    [StoreLimit(UpstreamRepository.EfCore, Source, 306, 307,
        Justification = Upstream.GaveNoReason)]
    public override Task ToOffset()
        => AssertTranslationFailed(base.ToOffset);

    [StoreLimit(UpstreamRepository.EfCore, Source, 309, 310,
        Justification = Upstream.GaveNoReason)]
    public override Task Ctor_DateTime()
        => AssertTranslationFailed(base.Ctor_DateTime);

    [StoreLimit(UpstreamRepository.EfCore, Source, 312, 313,
        Justification = Upstream.GaveNoReason)]
    public override Task Ctor_DateTime_TimeSpan()
        => AssertTranslationFailed(base.Ctor_DateTime_TimeSpan);

}
