// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DatabaseCleanupTest
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Cleanup_failure_is_reported_without_retry(HttpStatusCode status)
    {
        int attempts = 0;
        var expected = new CosmosException("cleanup failure", status, 0, "activity-proof", 0);
        var messages = new List<string>();
        var actual = await Assert.ThrowsAsync<CosmosException>(() => CosmosEmulator.RunDatabaseCleanupAsync(() =>
        {
            attempts++;
            throw expected;
        }, "database-proof", "fixture-proof", TestContext.Current.CancellationToken, messages.Add));
        Assert.Same(expected, actual);
        Assert.Equal(1, attempts);
        string diagnostic = Assert.Single(messages, m => m.Contains("FAILED", StringComparison.Ordinal));
        Assert.Contains("database-proof", diagnostic, StringComparison.Ordinal);
        Assert.Contains("fixture-proof", diagnostic, StringComparison.Ordinal);
        Assert.Contains("cleanup failure", diagnostic, StringComparison.Ordinal);
        Assert.Contains("activity-proof", diagnostic, StringComparison.Ordinal);
        Assert.Contains(nameof(CosmosException), diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cleanup_diagnostic_includes_inner_exception_and_preserves_exception_identity()
    {
        var expected = new InvalidOperationException("outer cleanup failure", new IOException("inner cleanup failure"));
        var messages = new List<string>();
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => CosmosEmulator.RunDatabaseCleanupAsync(
            () => throw expected, "database-proof", "fixture-proof", TestContext.Current.CancellationToken, messages.Add));
        Assert.Same(expected, actual);
        string diagnostic = Assert.Single(messages, m => m.Contains("FAILED", StringComparison.Ordinal));
        Assert.Contains("System.InvalidOperationException: outer cleanup failure", diagnostic, StringComparison.Ordinal);
        Assert.Contains("System.IO.IOException: inner cleanup failure", diagnostic, StringComparison.Ordinal);
        Assert.Contains(nameof(Cleanup_diagnostic_includes_inner_exception_and_preserves_exception_identity), diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_stops_cleanup_before_the_first_attempt()
    {
        int attempts = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CosmosEmulator.RunDatabaseCleanupAsync(() =>
        {
            attempts++;
            return Task.CompletedTask;
        }, "cancelled-database", "cancelled-fixture", cancellation.Token, _ => { }));
        Assert.Equal(0, attempts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Independent_databases_are_deleted_concurrently(int round)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await CosmosEmulator.EnsureAvailableAsync(cancellationToken);
        using var client = CosmosEmulator.Client();
        string[] names = Enumerable.Range(0, 8).Select(i => $"icc-cleanup-{round}-{i}-{Guid.NewGuid():N}").ToArray();
        try
        {
            foreach (string name in names)
            {
                DatabaseResponse database = await client.CreateDatabaseAsync(name, cancellationToken: cancellationToken);
                await database.Database.CreateContainerAsync("Documents", "/id", cancellationToken: cancellationToken);
            }
            await Task.WhenAll(names.Select(name => CosmosEmulator.DeleteDatabaseAsync(client, name, cancellationToken, nameof(DatabaseCleanupTest))));
            foreach (string name in names)
            {
                var error = await Assert.ThrowsAsync<CosmosException>(() =>
                    client.GetDatabase(name).ReadAsync(cancellationToken: cancellationToken));
                Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
            }
        }
        finally
        {
            foreach (string name in names)
                await CosmosEmulator.DeleteDatabaseAsync(client, name, cancellationToken, nameof(DatabaseCleanupTest));
        }
    }
}
