// Licensed under the MIT license. See license.txt file in the project root for license information.

using Testcontainers.CosmosDb;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

[assembly: AssemblyFixture(typeof(InfoCarrier.Core.CosmosTests.CosmosContainerFixture))]

namespace InfoCarrier.Core.CosmosTests;

public sealed class CosmosContainerFixture : IAsyncLifetime
{
    private CosmosDbContainer? _container;
    private CosmosQueryCapture? _queries;
    private readonly string _logs = Path.Combine(RepositoryRoot(), "artifacts", "cosmos-emulator", Environment.ProcessId.ToString());

    [InfoCarrierDesign(9, Deviation = DeviationKind.Other,
        Justification = "The document tier owns its Linux emulator lifecycle and retains diagnostics across both host operating systems.",
        DeviationNote = "Own one fresh assembly container rather than automatically reusing localhost. External endpoints require explicit opt-in. Keep diagnostic capture and a three-minute startup deadline; use the official CosmosDbBuilder defaults without package changes.")]
    public async ValueTask InitializeAsync()
    {
        if (InfoCarrier.Core.FunctionalTests.TestUtilities.LiveComparison.IsEnabled)
            _queries = new CosmosQueryCapture();
        // An explicit endpoint is an opt-in to an externally managed emulator.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INFOCARRIER_COSMOS_ENDPOINT")))
        {
            await CosmosEmulator.EnsureAvailableAsync(TestContext.Current.CancellationToken);
            return;
        }
        try
        {
            _container = new CosmosDbBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest")
                .WithLabel("io.infocarrier.cosmos-tests", "true")
                .Build();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            await _container.StartAsync(deadline.Token);
            CosmosEmulator.ManagedEndpoint = new UriBuilder(Uri.UriSchemeHttp,
                _container.Hostname, _container.GetMappedPublicPort(CosmosDbBuilder.CosmosDbPort)).ToString();
            CosmosEmulator.ManagedHttpMessageHandler = _container.HttpMessageHandler;
            Directory.CreateDirectory(_logs);
            await File.WriteAllTextAsync(Path.Combine(_logs, "container.txt"),
                $"id={_container.Id}\nendpoint={CosmosEmulator.Endpoint}\n", TestContext.Current.CancellationToken);
            Console.Error.WriteLine($"Cosmos Testcontainers emulator ready: {CosmosEmulator.Endpoint}");
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(_logs);
            await File.WriteAllTextAsync(Path.Combine(_logs, "startup.log"), error.ToString());
            try { await DisposeAsync(); }
            catch (Exception cleanup) { Console.Error.WriteLine($"Cosmos startup cleanup failed: {cleanup}"); }
            throw new InvalidOperationException(
                "Cannot start the Cosmos test container. Start Docker with Linux containers, or run 'podman machine start <name>'. "
                + "Use 'podman machine list' to find the machine name. "
                + "Testcontainers connects through the container API; the docker command is not required. "
                + "Check DOCKER_HOST or your active Docker context when using a non-default endpoint.", error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queries?.Dispose();
        _queries = null;
        if (_container is null) return;
        var container = _container;
        _container = null;
        try
        {
            Directory.CreateDirectory(_logs);
            foreach (string name in new[] { "pglog.log", "package-updates.log", "start_postgres_server.log", "postgresql-packages.log" })
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    string[] command = name == "postgresql-packages.log"
                        ? ["rpm", "-q", "postgresql", "postgresql-libs"] : ["cat", "/logs/" + name];
                    var result = await container.ExecAsync(command, deadline.Token);
                    await File.WriteAllTextAsync(Path.Combine(_logs, name),
                        result.Stdout + result.Stderr + $"\nCapture exit code: {result.ExitCode}\n");
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine($"Cannot capture Cosmos internal log {name}: {error.Message}");
                }
            }
            var (stdout, stderr) = await container.GetLogsAsync();
            await File.WriteAllTextAsync(Path.Combine(_logs, "stdout.log"), stdout);
            await File.WriteAllTextAsync(Path.Combine(_logs, "stderr.log"), stderr);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Cannot capture Cosmos container logs: {error.Message}");
        }
        finally
        {
            await container.DisposeAsync();
            CosmosEmulator.ManagedEndpoint = null;
            CosmosEmulator.ManagedHttpMessageHandler = null;
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "InfoCarrier.Core.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Cannot locate the repository for Cosmos container logs.");
    }
}
