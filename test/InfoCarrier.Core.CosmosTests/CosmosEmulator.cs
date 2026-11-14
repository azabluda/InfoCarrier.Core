// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.CosmosTests;

internal static class CosmosEmulator
{
    [ModuleInitializer]
    internal static void ConfigureQueryExecution()
    {
        // The pinned emulator misclassifies nested aggregates in gateway query plans.
        // Use the SDK's supported single-physical-partition execution mode on every OS.
        // Keep gateway planning selected on Windows too, so local runs expose Linux failures
        // if a query cannot use optimistic direct execution and falls back to planning.
        // https://learn.microsoft.com/azure/cosmos-db/performance-tips-query-sdk#optimize-single-partition-queries-with-optimistic-direct-execution
        Environment.SetEnvironmentVariable("AZURE_COSMOS_BYPASS_QUERY_PARSING", "true");
        Environment.SetEnvironmentVariable("AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED", "true");
    }

    // Microsoft's public emulator key, safe to commit. Never use an Azure account in this project.
    // https://learn.microsoft.com/en-us/azure/cosmos-db/emulator#authentication
    internal const string Key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    internal static string Endpoint => Environment.GetEnvironmentVariable("INFOCARRIER_COSMOS_ENDPOINT") ?? "https://localhost:8081";

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
}
