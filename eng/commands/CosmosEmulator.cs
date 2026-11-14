// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;

internal static class CosmosEmulatorCommand
{
    internal const string Image = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator@sha256:81707bcadf894a29d7dab63377685d408b5d54dca3aacfb625c19db4c6392e7c";
    private const string Name = "infocarrier-cosmos-tests";
    private const string OwnerLabel = "io.infocarrier.cosmos-tests";

    internal static async Task<int> Run(string[] args, CancellationToken token)
    {
        if (args.Length != 2 || args[1] is not ("podman" or "docker") || args[0] is not ("start" or "stop" or "logs"))
        {
            Console.Error.WriteLine("usage: CosmosEmulator.cs start|stop|logs podman|docker");
            return 2;
        }
        string runtime = args[1];
        async Task<ScriptHost.ProcessResult> Execute(params string[] command)
        {
            var result = await ScriptHost.Execute(runtime, command, ScriptHost.Root, true, token);
            if (result.Output.Length > 0) Console.Write(result.Output);
            if (result.Error.Length > 0) Console.Error.Write(result.Error);
            return result;
        }
        if (args[0] == "logs") return (await Execute("logs", Name)).ExitCode;
        if (args[0] == "stop")
        {
            var exists = await ScriptHost.Execute(runtime, ["inspect", "--format", "{{ index .Config.Labels \"" + OwnerLabel + "\" }}", Name],
                ScriptHost.Root, true, token);
            if (exists.ExitCode != 0) return 0;
            if (exists.Output.Trim() != "true")
            {
                Console.Error.WriteLine("Refusing to remove a container without this test harness's ownership label.");
                return 1;
            }
            return (await Execute("rm", "--force", Name)).ExitCode;
        }
        var start = await Execute("run", "--detach", "--name", Name, "--label", OwnerLabel + "=true",
            "--publish", "127.0.0.1:8081:8081", "--publish", "127.0.0.1:8080:8080",
            "--env", "ENABLE_EXPLORER=false", "--env", "ENABLE_TELEMETRY=false", Image, "--protocol", "https");
        if (start.ExitCode != 0) return start.ExitCode;

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromMinutes(3))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync("http://localhost:8080/ready", token);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Cosmos emulator ready: https://localhost:8081");
                    return 0;
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!token.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromSeconds(1), token);
        }
        Console.Error.WriteLine("Cosmos emulator did not become ready within three minutes.");
        await Execute("logs", Name);
        return 1;
    }
}
