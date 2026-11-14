// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class TimeOnlyTest(BasicTypesFixture fixture) : TimeOnlyTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 17, 18,
        Justification = Upstream.GaveNoReason)]
    public override Task Hour()
        => AssertTranslationFailed(base.Hour);

    [StoreLimit(UpstreamRepository.EfCore, Source, 20, 21,
        Justification = Upstream.GaveNoReason)]
    public override Task Minute()
        => AssertTranslationFailed(base.Minute);

    [StoreLimit(UpstreamRepository.EfCore, Source, 23, 24,
        Justification = Upstream.GaveNoReason)]
    public override Task Second()
        => AssertTranslationFailed(base.Second);

    [StoreLimit(UpstreamRepository.EfCore, Source, 26, 27,
        Justification = Upstream.GaveNoReason)]
    public override Task Millisecond()
        => AssertTranslationFailed(base.Millisecond);

    [StoreLimit(UpstreamRepository.EfCore, Source, 29, 30,
        Justification = Upstream.GaveNoReason)]
    public override Task Microsecond()
        => AssertTranslationFailed(base.Microsecond);

    [StoreLimit(UpstreamRepository.EfCore, Source, 32, 33,
        Justification = Upstream.GaveNoReason)]
    public override Task Nanosecond()
        => AssertTranslationFailed(base.Nanosecond);

    [StoreLimit(UpstreamRepository.EfCore, Source, 35, 36,
        Justification = Upstream.GaveNoReason)]
    public override Task AddHours()
        => AssertTranslationFailed(base.AddHours);

    [StoreLimit(UpstreamRepository.EfCore, Source, 38, 39,
        Justification = Upstream.GaveNoReason)]
    public override Task AddMinutes()
        => AssertTranslationFailed(base.AddMinutes);

    [StoreLimit(UpstreamRepository.EfCore, Source, 41, 42,
        Justification = Upstream.GaveNoReason)]
    public override Task Add_TimeSpan()
        => AssertTranslationFailed(base.Add_TimeSpan);

    [StoreLimit(UpstreamRepository.EfCore, Source, 44, 45,
        Justification = Upstream.GaveNoReason)]
    public override Task IsBetween()
        => AssertTranslationFailed(base.IsBetween);

    [StoreIssue(IssueTracker.EfCore, 35311, UpstreamRepository.EfCore, Source, 47, 58,
        Justification = "See #35311")]
    public override async Task Subtract()
    {
        // See #35311
        await Assert.ThrowsAsync<EqualException>(base.Subtract);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 60, 61,
        Justification = Upstream.GaveNoReason)]
    public override Task FromDateTime_compared_to_property()
        => AssertTranslationFailed(base.FromDateTime_compared_to_property);

    [StoreLimit(UpstreamRepository.EfCore, Source, 63, 64,
        Justification = Upstream.GaveNoReason)]
    public override Task FromDateTime_compared_to_parameter()
        => AssertTranslationFailed(base.FromDateTime_compared_to_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 66, 67,
        Justification = Upstream.GaveNoReason)]
    public override Task FromDateTime_compared_to_constant()
        => AssertTranslationFailed(base.FromDateTime_compared_to_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 69, 70,
        Justification = Upstream.GaveNoReason)]
    public override Task FromTimeSpan_compared_to_property()
        => AssertTranslationFailed(base.FromTimeSpan_compared_to_property);

    [StoreLimit(UpstreamRepository.EfCore, Source, 72, 73,
        Justification = Upstream.GaveNoReason)]
    public override Task FromTimeSpan_compared_to_parameter()
        => AssertTranslationFailed(base.FromTimeSpan_compared_to_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 75, 76,
        Justification = Upstream.GaveNoReason)]
    public override Task Order_by_FromTimeSpan()
        => AssertTranslationFailed(base.Order_by_FromTimeSpan);

}
