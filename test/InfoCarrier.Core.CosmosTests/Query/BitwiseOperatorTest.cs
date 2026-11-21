// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class BitwiseOperatorTest(BasicTypesFixture fixture) : BitwiseOperatorTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/Operators/BitwiseOperatorTranslationsCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 34, 45,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task Or_over_boolean()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.Or_over_boolean);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 76, 87,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task And_over_boolean()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.And_over_boolean);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 130, 141,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task And_or_over_boolean()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.And_or_over_boolean);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 143, 154,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task Or_with_logical_or()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.Or_with_logical_or);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 156, 167,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task And_with_logical_and()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.And_with_logical_and);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 169, 180,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task Or_with_logical_and()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.Or_with_logical_and);
    }

    [StoreIssue(IssueTracker.EfCore, 13168, UpstreamRepository.EfCore, Source, 182, 193,
        Justification = "Bitwise operators on booleans. Issue #13168.")]
    public override async Task And_with_logical_or()
    {
        // Bitwise operators on booleans. Issue #13168.
        await Assert.ThrowsAsync<EqualException>(base.And_with_logical_or);
    }

}
