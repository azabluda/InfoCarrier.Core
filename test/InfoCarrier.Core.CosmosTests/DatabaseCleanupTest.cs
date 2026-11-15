// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DatabaseCleanupTest
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public async Task Transient_cleanup_failure_retries_only_until_success(int failures)
    {
        int attempts = 0;
        await CosmosEmulator.RetryDatabaseCleanupAsync(() =>
        {
            if (attempts++ < failures)
                throw new CosmosException("catalog deadlock", HttpStatusCode.InternalServerError, 0, "test", 0);
            return Task.CompletedTask;
        }, TestContext.Current.CancellationToken);
        Assert.Equal(failures + 1, attempts);
    }

    [Fact]
    public async Task Persistent_cleanup_failure_is_reported()
    {
        int attempts = 0;
        var expected = new CosmosException("persistent failure", HttpStatusCode.InternalServerError, 0, "test", 0);
        var actual = await Assert.ThrowsAsync<CosmosException>(() => CosmosEmulator.RetryDatabaseCleanupAsync(() =>
        {
            attempts++;
            throw expected;
        }, TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task Other_failures_are_not_retried()
    {
        int attempts = 0;
        var expected = new CosmosException("forbidden", HttpStatusCode.Forbidden, 0, "test", 0);
        var actual = await Assert.ThrowsAsync<CosmosException>(() => CosmosEmulator.RetryDatabaseCleanupAsync(() =>
        {
            attempts++;
            throw expected;
        }, TestContext.Current.CancellationToken));
        Assert.Same(expected, actual);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Cancellation_stops_cleanup_before_the_first_attempt()
    {
        int attempts = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CosmosEmulator.RetryDatabaseCleanupAsync(() =>
        {
            attempts++;
            return Task.CompletedTask;
        }, cancellation.Token));
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
            await Task.WhenAll(names.Select(name => CosmosEmulator.DeleteDatabaseAsync(client, name, cancellationToken)));
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
                await CosmosEmulator.DeleteDatabaseAsync(client, name, cancellationToken);
        }
    }
}

