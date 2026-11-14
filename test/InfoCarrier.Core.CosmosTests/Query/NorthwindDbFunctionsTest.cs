// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindDbFunctionsTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindDbFunctionsQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindDbFunctionsQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 18, 23, Justification = Upstream.GaveNoReason)]
    public override async Task Like_all_literals(bool async)
    {
        await AssertTranslationFailed(() => base.Like_all_literals(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 25, 30, Justification = Upstream.GaveNoReason)]
    public override async Task Like_all_literals_with_escape(bool async)
    {
        await AssertTranslationFailed(() => base.Like_all_literals_with_escape(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 32, 37, Justification = Upstream.GaveNoReason)]
    public override async Task Like_literal(bool async)
    {
        await AssertTranslationFailed(() => base.Like_literal(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 39, 44, Justification = Upstream.GaveNoReason)]
    public override async Task Like_literal_with_escape(bool async)
    {
        await AssertTranslationFailed(() => base.Like_literal_with_escape(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 46, 51, Justification = Upstream.GaveNoReason)]
    public override async Task Like_identity(bool async)
    {
        await AssertTranslationFailed(() => base.Like_identity(async));
    }
}
