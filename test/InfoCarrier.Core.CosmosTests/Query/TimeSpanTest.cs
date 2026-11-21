// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

namespace InfoCarrier.Core.CosmosTests.Query;

public class TimeSpanTest(BasicTypesFixture fixture) : TimeSpanTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 15, 16,
        Justification = Upstream.GaveNoReason)]
    public override Task Hours()
        => AssertTranslationFailed(base.Hours);

    [StoreLimit(UpstreamRepository.EfCore, Source, 18, 19,
        Justification = Upstream.GaveNoReason)]
    public override Task Minutes()
        => AssertTranslationFailed(base.Minutes);

    [StoreLimit(UpstreamRepository.EfCore, Source, 21, 22,
        Justification = Upstream.GaveNoReason)]
    public override Task Seconds()
        => AssertTranslationFailed(base.Seconds);

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 25,
        Justification = Upstream.GaveNoReason)]
    public override Task Milliseconds()
        => AssertTranslationFailed(base.Milliseconds);

    [StoreLimit(UpstreamRepository.EfCore, Source, 27, 28,
        Justification = Upstream.GaveNoReason)]
    public override Task Microseconds()
        => AssertTranslationFailed(base.Microseconds);

    [StoreLimit(UpstreamRepository.EfCore, Source, 30, 31,
        Justification = Upstream.GaveNoReason)]
    public override Task Nanoseconds()
        => AssertTranslationFailed(base.Nanoseconds);

}
