// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindFunctionsTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindFunctionsQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindFunctionsQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 36,
        Justification = "Cosmos does not support synchronous operations.")]
    public override Task Client_evaluation_of_uncorrelated_method_call(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Client_evaluation_of_uncorrelated_method_call(a);
            });

    [StoreIssue(IssueTracker.EfCore, 27037, UpstreamRepository.EfCore, Source, 38, 53,
        Justification = "Unsupported ORDER BY clause. Issue #27037.",
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.Other,
        DeviationNote = "Assert the remote CosmosException. Also execute the synchronous refusal that upstream leaves empty.")]
    public override Task Order_by_length_twice(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (!a)
            {
                await base.Order_by_length_twice(false);
                return;
            }
            InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(() => base.Order_by_length_twice(a));
            Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
            Assert.Contains("Unsupported ORDER BY clause.", error.Message, StringComparison.Ordinal);
        });

    [StoreLimit(UpstreamRepository.EfCore, Source, 55, 65,
        Justification = "Including navigation is not supported as the navigation is not embedded in same resource.")]
    public override async Task Order_by_length_twice_followed_by_projection_of_naked_collection_navigation(bool async)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Order_by_length_twice_followed_by_projection_of_naked_collection_navigation(async));
        Assert.Equal(
            "Including navigation 'Navigation: Customer.Orders (List<Order>) Collection ToDependent Order Inverse: Customer PropertyAccessMode.Field' is not supported as the navigation is not embedded in same resource.",
            ex.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 67, 68,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Sum_over_round_works_correctly_in_projection(bool async)
        => AssertTranslationFailed(() => base.Sum_over_round_works_correctly_in_projection(async));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 70, 71,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Sum_over_round_works_correctly_in_projection_2(bool async)
        => AssertTranslationFailed(() => base.Sum_over_round_works_correctly_in_projection_2(async));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 73, 74,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Sum_over_truncate_works_correctly_in_projection(bool async)
        => AssertTranslationFailed(() => base.Sum_over_truncate_works_correctly_in_projection(async));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 76, 77,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Sum_over_truncate_works_correctly_in_projection_2(bool async)
        => AssertTranslationFailed(() => base.Sum_over_truncate_works_correctly_in_projection_2(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 79, 91,
        Justification = "Cosmos does not support synchronous operations.")]
    public override Task Where_functions_nested(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Where_functions_nested(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 93, 107,
        Justification = "Cosmos does not support synchronous operations.")]
    public override Task Static_equals_nullable_datetime_compared_to_non_nullable(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Static_equals_nullable_datetime_compared_to_non_nullable(a);
            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 109, 121,
        Justification = "Cosmos does not support synchronous operations.")]
    public override Task Static_equals_int_compared_to_long(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Static_equals_int_compared_to_long(a);
            });

}
