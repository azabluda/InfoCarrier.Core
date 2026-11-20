// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.CosmosTests;

internal static class CosmosEmulator
{
    // Microsoft's public emulator key, safe to commit. Never use an Azure account in this project.
    // https://learn.microsoft.com/en-us/azure/cosmos-db/emulator#authentication
    internal const string Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    internal static string? ManagedEndpoint { get; set; }
    internal static string Endpoint => Environment.GetEnvironmentVariable("INFOCARRIER_COSMOS_ENDPOINT")
        ?? ManagedEndpoint ?? throw new InvalidOperationException("The Cosmos assembly container fixture has not initialized.");

    internal static async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // The assembly fixture already awaited the managed container's readiness once.
        // Explicit external endpoints still need the diagnostic reachability check.
        if (ManagedEndpoint is not null) return;
        using HttpClient client = HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        await EnsureAvailableAsync(client, new Uri(Endpoint), cancellationToken);
    }

    internal static async Task EnsureAvailableAsync(HttpClient client, Uri endpoint, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Any HTTP response confirms reachability. Authentication and service errors belong
            // to the provider, rather than being mislabeled as a missing container.
            using HttpResponseMessage response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested
            && (error is HttpRequestException or OperationCanceledException))
        {
            throw new InvalidOperationException(
                $"Cannot reach the Cosmos emulator at {endpoint}.\n"
                + "Testcontainers starts and stops the Cosmos emulator for each test run.\n"
                + "If INFOCARRIER_COSMOS_ENDPOINT selects an external emulator, start that emulator yourself.\n"
                + "For Podman, start its machine first: podman machine start\n"
                + "Start your container runtime with Linux containers, then run from the repository root:\n"
                + "  dotnet run --file eng/CosmosEmulator.cs -- start podman\n"
                + "or:\n"
                + "  dotnet run --file eng/CosmosEmulator.cs -- start docker\n"
                + "Wait for 'Cosmos emulator ready', then run the tests again.", error);
        }
    }

    internal static HttpClient HttpClient()
    {
        var endpoint = new Uri(Endpoint);
        if (!endpoint.IsLoopback || endpoint.Scheme != "https")
            throw new InvalidOperationException("Cosmos tests require a loopback HTTPS emulator endpoint.");
        return new HttpClient(new HttpClientHandler
        {
            // The emulator generates its own certificate. This client cannot reach a remote endpoint.
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.IsLoopback == true,
        });
    }

    internal static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string database)
        => builder.UseCosmos(Endpoint, Key, database, options => options
            .ConnectionMode(ConnectionMode.Gateway)
            .HttpClientFactory(HttpClient));

    internal static CosmosClient Client() => new(Endpoint, Key, new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Gateway,
        HttpClientFactory = HttpClient,
    });

    internal static void ReportDatabaseOwner(string database, object owner, string logicalName)
        => Console.Error.WriteLine($"{DateTimeOffset.UtcNow:O} Cosmos database owner: database={database}; "
            + $"owner={owner.GetType().FullName}/{logicalName}@{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner):x}; "
            + $"class={Xunit.TestContext.Current.TestClass?.TestClassName ?? "unknown"}; process={Environment.ProcessId}");

    internal static Task DeleteDatabaseAsync(CosmosClient client, string database, CancellationToken cancellationToken,
        string owner)
        => RunDatabaseCleanupAsync(async () =>
        {
            try
            {
                await client.GetDatabase(database).DeleteAsync(cancellationToken: cancellationToken);
            }
            catch (CosmosException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        }, database, owner, cancellationToken);

    internal static async Task RunDatabaseCleanupAsync(Func<Task> delete, string database, string owner,
        CancellationToken cancellationToken, Action<string>? report = null)
    {
        report ??= Console.Error.WriteLine;
        string identity = $"database={database}; owner={owner}; class={Xunit.TestContext.Current.TestClass?.TestClassName ?? "unknown"}; process={Environment.ProcessId}";
        report($"{DateTimeOffset.UtcNow:O} Cosmos database cleanup: {identity}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await delete();
            report($"{DateTimeOffset.UtcNow:O} Cosmos database cleanup completed: {identity}");
        }
        catch (Exception error)
        {
            // Include the complete exception chain even when the runner truncates fixture failures.
            report($"{DateTimeOffset.UtcNow:O} Cosmos database cleanup FAILED: {identity}\n{error}");
            throw;
        }
    }
}
