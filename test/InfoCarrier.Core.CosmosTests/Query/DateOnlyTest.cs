// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;

namespace InfoCarrier.Core.CosmosTests.Query;

public class DateOnlyTest(BasicTypesFixture fixture) : DateOnlyTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 15, 16,
        Justification = Upstream.GaveNoReason)]
    public override Task Year()
        => AssertTranslationFailed(base.Year);

    [StoreLimit(UpstreamRepository.EfCore, Source, 18, 19,
        Justification = Upstream.GaveNoReason)]
    public override Task Month()
        => AssertTranslationFailed(base.Month);

    [StoreLimit(UpstreamRepository.EfCore, Source, 21, 22,
        Justification = Upstream.GaveNoReason)]
    public override Task Day()
        => AssertTranslationFailed(base.Day);

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 25,
        Justification = Upstream.GaveNoReason)]
    public override Task DayOfYear()
        => AssertTranslationFailed(base.DayOfYear);

    [StoreLimit(UpstreamRepository.EfCore, Source, 27, 28,
        Justification = Upstream.GaveNoReason)]
    public override Task DayOfWeek()
        => AssertTranslationFailed(base.DayOfWeek);

    [StoreLimit(UpstreamRepository.EfCore, Source, 30, 32,
        Justification = "Cosmos does not support DateTimeDiff with years under 1601")]
    // Cosmos does not support DateTimeDiff with years under 1601
    public override Task DayNumber()
        => AssertTranslationFailed(base.DayNumber);

    [StoreLimit(UpstreamRepository.EfCore, Source, 34, 35,
        Justification = Upstream.GaveNoReason)]
    public override Task AddYears()
        => AssertTranslationFailed(base.AddYears);

    [StoreLimit(UpstreamRepository.EfCore, Source, 37, 38,
        Justification = Upstream.GaveNoReason)]
    public override Task AddMonths()
        => AssertTranslationFailed(base.AddMonths);

    [StoreLimit(UpstreamRepository.EfCore, Source, 40, 41,
        Justification = Upstream.GaveNoReason)]
    public override Task AddDays()
        => AssertTranslationFailed(base.AddDays);

    [StoreLimit(UpstreamRepository.EfCore, Source, 43, 45,
        Justification = "Cosmos does not support DateTimeDiff with years under 1601")]
    // Cosmos does not support DateTimeDiff with years under 1601
    public override Task DayNumber_subtraction()
        => AssertTranslationFailed(base.DayNumber_subtraction);

    [StoreLimit(UpstreamRepository.EfCore, Source, 47, 48,
        Justification = Upstream.GaveNoReason)]
    public override Task FromDateTime()
        => AssertTranslationFailed(base.FromDateTime);

    [StoreLimit(UpstreamRepository.EfCore, Source, 50, 51,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call FromDateTime_compared_to_property; upstream calls FromDateTime.")]
    public override Task FromDateTime_compared_to_property()
        => AssertTranslationFailed(base.FromDateTime_compared_to_property);

    [StoreLimit(UpstreamRepository.EfCore, Source, 53, 54,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call FromDateTime_compared_to_constant_and_parameter; upstream calls FromDateTime.")]
    public override Task FromDateTime_compared_to_constant_and_parameter()
        => AssertTranslationFailed(base.FromDateTime_compared_to_constant_and_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 56, 57,
        Justification = Upstream.GaveNoReason)]
    public override Task ToDateTime_property_with_constant_TimeOnly()
        => AssertTranslationFailed(base.ToDateTime_property_with_constant_TimeOnly);

    [StoreLimit(UpstreamRepository.EfCore, Source, 59, 60,
        Justification = Upstream.GaveNoReason)]
    public override Task ToDateTime_property_with_property_TimeOnly()
        => AssertTranslationFailed(base.ToDateTime_property_with_property_TimeOnly);

    [StoreLimit(UpstreamRepository.EfCore, Source, 62, 63,
        Justification = Upstream.GaveNoReason)]
    public override Task ToDateTime_constant_DateTime_with_property_TimeOnly()
        => AssertTranslationFailed(base.ToDateTime_constant_DateTime_with_property_TimeOnly);

    [StoreLimit(UpstreamRepository.EfCore, Source, 65, 66,
        Justification = Upstream.GaveNoReason)]
    public override Task ToDateTime_with_complex_DateTime()
        => AssertTranslationFailed(base.ToDateTime_with_complex_DateTime);

    [StoreLimit(UpstreamRepository.EfCore, Source, 68, 69,
        Justification = Upstream.GaveNoReason)]
    public override Task ToDateTime_with_complex_TimeOnly()
        => AssertTranslationFailed(base.ToDateTime_with_complex_TimeOnly);

}
