// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace InfoCarrier.Core.CosmosTests.Query;

public class ByteArrayTest(BasicTypesFixture fixture) : ByteArrayTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/ByteArrayTranslationsCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 15, 16,
        Justification = Upstream.GaveNoReason)]
    public override Task Length()
        => AssertTranslationFailed(base.Length);

    [StoreLimit(UpstreamRepository.EfCore, Source, 18, 19,
        Justification = Upstream.GaveNoReason)]
    public override Task Index()
        => AssertTranslationFailed(base.Index);

    [StoreLimit(UpstreamRepository.EfCore, Source, 21, 22,
        Justification = Upstream.GaveNoReason)]
    public override Task First()
        => AssertTranslationFailed(base.First);

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 25,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_constant()
        => AssertTranslationFailed(base.Contains_with_constant);

    [StoreLimit(UpstreamRepository.EfCore, Source, 27, 28,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_parameter()
        => AssertTranslationFailed(base.Contains_with_parameter);

    [StoreLimit(UpstreamRepository.EfCore, Source, 30, 31,
        Justification = Upstream.GaveNoReason)]
    public override Task Contains_with_column()
        => AssertTranslationFailed(base.Contains_with_column);

    [StoreLimit(UpstreamRepository.EfCore, Source, 33, 34,
        Justification = Upstream.GaveNoReason)]
    public override Task Any()
        => AssertTranslationFailed(base.Any);

    [StoreLimit(UpstreamRepository.EfCore, Source, 36, 37,
        Justification = Upstream.GaveNoReason)]
    public override Task SequenceEqual()
        => AssertTranslationFailed(base.SequenceEqual);

}
