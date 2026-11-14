// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

[WireFreeControl]
public class EmulatorAvailabilityTest
{
    private static readonly Uri Endpoint = new("https://localhost:8081");

    [Fact]
    public async Task A_connection_failure_explains_how_to_start_the_emulator()
    {
        var cause = new HttpRequestException("Connection refused");
        using var client = new HttpClient(new ReplyHandler(() => throw cause));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CosmosEmulator.EnsureAvailableAsync(client, Endpoint, TestContext.Current.CancellationToken));

        Assert.Same(cause, error.InnerException);
        Assert.Contains(Endpoint.ToString(), error.Message);
        Assert.Contains("fixtures do not start containers", error.Message);
        Assert.Contains("dotnet run --file eng/CosmosEmulator.cs -- start podman", error.Message);
        Assert.Contains("dotnet run --file eng/CosmosEmulator.cs -- start docker", error.Message);
    }

    [Fact]
    public async Task A_connection_timeout_has_the_startup_instructions()
    {
        using var client = new HttpClient(new ReplyHandler(() => throw new TaskCanceledException("Request timed out")));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CosmosEmulator.EnsureAvailableAsync(client, Endpoint, TestContext.Current.CancellationToken));

        Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.Contains("Cannot reach the Cosmos emulator", error.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task A_response_leaves_provider_validation_to_the_provider(HttpStatusCode status)
    {
        using var client = new HttpClient(new ReplyHandler(() => new HttpResponseMessage(status)));
        await CosmosEmulator.EnsureAvailableAsync(client, Endpoint, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Starting_the_emulator_allows_retry_in_the_same_process()
    {
        int attempts = 0;
        using var client = new HttpClient(new ReplyHandler(() => ++attempts == 1
            ? throw new HttpRequestException("Connection refused")
            : new HttpResponseMessage(HttpStatusCode.OK)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CosmosEmulator.EnsureAvailableAsync(client, Endpoint, TestContext.Current.CancellationToken));
        await CosmosEmulator.EnsureAvailableAsync(client, Endpoint, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Test_cancellation_is_not_reported_as_a_missing_emulator()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var client = new HttpClient(new ReplyHandler(() => new HttpResponseMessage(HttpStatusCode.OK)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CosmosEmulator.EnsureAvailableAsync(client, Endpoint, cancellation.Token));
    }

    private sealed class ReplyHandler(Func<HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(reply());
    }
}
