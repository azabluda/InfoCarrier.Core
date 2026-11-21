// Licensed under the MIT license. See license.txt file in the project root for license information.

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

[assembly: AssemblyFixture(typeof(InfoCarrier.Core.CosmosTests.CosmosContainerFixture))]

namespace InfoCarrier.Core.CosmosTests;

public sealed class CosmosContainerFixture : IAsyncLifetime
{
    private IContainer? _container;
    private readonly string _logs = Path.Combine(RepositoryRoot(), "artifacts", "cosmos-emulator", Environment.ProcessId.ToString());

    public async ValueTask InitializeAsync()
    {
        // An explicit endpoint is an opt-in to an externally managed emulator.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INFOCARRIER_COSMOS_ENDPOINT")))
        {
            await CosmosEmulator.EnsureAvailableAsync(TestContext.Current.CancellationToken);
            return;
        }
        try
        {
            _container = new ContainerBuilder("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:81707bcadf894a29d7dab63377685d408b5d54dca3aacfb625c19db4c6392e7c")
                .WithLabel("io.infocarrier.cosmos-tests", "true")
                .WithEnvironment("ENABLE_EXPLORER", "false")
                .WithEnvironment("ENABLE_TELEMETRY", "false")
                .WithCommand("--protocol", "https")
                .WithPortBinding(8081, true)
                .WithPortBinding(8080, true)
                .WithCreateParameterModifier(parameters =>
                {
                    foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                        foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
                })
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath("/ready")))
                .Build();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(3));
            await _container.StartAsync(deadline.Token);
            CosmosEmulator.ManagedEndpoint = $"https://localhost:{_container.GetMappedPublicPort(8081)}";
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
        if (_container is null) return;
        var container = _container;
        _container = null;
        try
        {
            Directory.CreateDirectory(_logs);
            foreach (string name in new[] { "pglog.log", "package-updates.log", "start_postgres_server.log" })
            {
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    var result = await container.ExecAsync(["cat", "/logs/" + name], deadline.Token);
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
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "InfoCarrier.Core.slnx"))) return directory.FullName;
        throw new InvalidOperationException("Cannot locate the repository for Cosmos container logs.");
    }
}
