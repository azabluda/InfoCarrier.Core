// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;

namespace InfoCarrier.EngineeringTests;

public sealed class RequiredJobsTest
{
    [Fact]
    public void Every_required_job_must_succeed()
    {
        using var error = new StringWriter();
        Assert.Equal(0, RequireSuccessfulJobs.Run(["functional=success", "cosmos=success"], error));
        Assert.Equal("", error.ToString());
    }

    [Theory]
    [InlineData("failure")]
    [InlineData("cancelled")]
    [InlineData("skipped")]
    [InlineData("unknown")]
    public void An_unsuccessful_dependency_cannot_produce_a_green_aggregate(string result)
    {
        using var error = new StringWriter();
        Assert.Equal(1, RequireSuccessfulJobs.Run(["functional=success", "cosmos=" + result], error));
        Assert.Contains("cosmos", error.ToString());
        Assert.Contains(result, error.ToString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("=success")]
    [InlineData("cosmos=")]
    public void An_incomplete_job_expression_is_an_error(string argument)
    {
        using var error = new StringWriter();
        Assert.Equal(2, RequireSuccessfulJobs.Run([argument], error));
    }

    [Fact]
    public void Missing_dependencies_are_an_error()
    {
        using var error = new StringWriter();
        Assert.Equal(2, RequireSuccessfulJobs.Run([], error));
    }

    [Fact]
    public void A_later_success_does_not_hide_an_earlier_failure()
    {
        using var error = new StringWriter();
        Assert.Equal(1, RequireSuccessfulJobs.Run(["functional=failure", "cosmos=success"], error));
        Assert.Contains("functional", error.ToString());
    }
}
