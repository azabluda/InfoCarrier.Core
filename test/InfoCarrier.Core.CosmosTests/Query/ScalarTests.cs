// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;

namespace InfoCarrier.Core.CosmosTests.Query;

public class GuidTest(BasicTypesFixture fixture) : GuidTranslationsTestBase<BasicTypesFixture>(fixture)
{
    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Query/Translations/GuidTranslationsCosmosTest.cs", 52, 58,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task NewGuid() => AssertTranslationFailed(base.NewGuid);
}

public class EnumTest(BasicTypesFixture fixture) : EnumTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/EnumTranslationsCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 35317, UpstreamRepository.EfCore, Source, 243, 245,
        Justification = "#35317")]
    public override Task HasFlag() => AssertTranslationFailed(base.HasFlag);

    [StoreIssue(IssueTracker.EfCore, 35317, UpstreamRepository.EfCore, Source, 247, 249,
        Justification = "#35317", Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call the parameterized base method; upstream calls its constant sibling.")]
    public override Task HasFlag_with_non_nullable_parameter()
        => AssertTranslationFailed(base.HasFlag_with_non_nullable_parameter);

    [StoreIssue(IssueTracker.EfCore, 35317, UpstreamRepository.EfCore, Source, 251, 253,
        Justification = "#35317", Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call the nullable-parameter base method; upstream calls its constant sibling.")]
    public override Task HasFlag_with_nullable_parameter()
        => AssertTranslationFailed(base.HasFlag_with_nullable_parameter);
}
