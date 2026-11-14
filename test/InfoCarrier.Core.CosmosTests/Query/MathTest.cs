// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Query;

public class MathTest(BasicTypesFixture fixture) : MathTranslationsTestBase<BasicTypesFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Translations/MathTranslationsCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 224, 230,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Round_with_digits_decimal()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Round_with_digits_decimal);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 232, 238,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Round_with_digits_double; upstream calls Round_with_digits_decimal.")]
    public override async Task Round_with_digits_double()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Round_with_digits_double);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 240, 246,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Round_with_digits_float; upstream calls Round_with_digits_decimal.")]
    public override async Task Round_with_digits_float()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Round_with_digits_float);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 299, 310,
        Justification = "Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and message through InfoCarrierServerException.")]
    public override async Task Truncate_project_and_order_by_it_twice()
    {
        InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Truncate_project_and_order_by_it_twice);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.", error.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 312, 323,
        Justification = "Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and message through InfoCarrierServerException.")]
    public override async Task Truncate_project_and_order_by_it_twice2()
    {
        InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Truncate_project_and_order_by_it_twice2);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.", error.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 325, 336,
        Justification = "Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and message through InfoCarrierServerException.")]
    public override async Task Truncate_project_and_order_by_it_twice3()
    {
        InfoCarrierServerException error = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Truncate_project_and_order_by_it_twice3);
        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, error.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("Unsupported ORDER BY clause. ORDER BY item expression could not be mapped to a document path.", error.Message, StringComparison.Ordinal);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 410, 416,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Log2()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Log2);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 510, 516,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Max()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Max);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 518, 524,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Max_nested; upstream calls Max.")]
    public override async Task Max_nested()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Max_nested);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 526, 532,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Max_nested_twice; upstream calls Max.")]
    public override async Task Max_nested_twice()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Max_nested_twice);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 534, 540,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Min()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Min);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 542, 548,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Min_nested; upstream calls Min.")]
    public override async Task Min_nested()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Min_nested);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 550, 556,
        Justification = "Cosmos client evaluation. Issue #17246.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call Min_nested_twice; upstream calls Min.")]
    public override async Task Min_nested_twice()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Min_nested_twice);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 632, 638,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Acosh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Acosh);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 664, 670,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Asinh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Asinh);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 696, 702,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Atanh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Atanh);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 740, 746,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Cosh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Cosh);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 772, 778,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Sinh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Sinh);
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 804, 810,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Tanh()
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(base.Tanh);
    }

}
