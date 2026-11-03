// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

internal static class CompatibilityCommand
{
    internal static async Task<int> Run(string[] args)
    {
        if (args is not ["--previous-client", string previousClient])
        {
            Console.Error.WriteLine("Usage: dotnet run --file eng/Compatibility.cs -- --previous-client <unchanged-client.dll>");
            return 2;
        }
        string current = Path.GetFullPath("tools/InfoCarrier.CompatibilityProbe/bin/Release/net10.0/InfoCarrier.CompatibilityProbe.dll");
        previousClient = Path.GetFullPath(previousClient);
        if (!File.Exists(previousClient) || !File.Exists(current))
        {
            Console.Error.WriteLine("Build the current probe and supply a preserved, unchanged earlier probe DLL.");
            return 2;
        }
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        string address = $"http://127.0.0.1:{port}";
        string database = Path.GetFullPath(Path.Combine("artifacts", "compatibility", $"probe-{Guid.NewGuid():N}.db"));
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        Console.WriteLine("Probe database: " + database);
        using Process server = Start(current, "server", address, database);
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                if (server.HasExited) { throw new InvalidOperationException("The probe server exited before readiness."); }
                try
                {
                    using HttpResponseMessage response = await http.GetAsync(address + "/ready", deadline.Token);
                    if (response.IsSuccessStatusCode) { break; }
                }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) when (!deadline.IsCancellationRequested) { }
                await Task.Delay(100, deadline.Token);
            }
            foreach (string client in new[] { previousClient, current })
            {
                Console.WriteLine("Separate client process: " + client);
                using Process process = Start(client, "client", address + "/infocarrier");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); } throw; }
                if (process.ExitCode != 0) { return process.ExitCode; }
            }
            foreach (string version in new[] { "3", "4" })
            {
                using Process metrics = Start(current, "metrics", version);
                await metrics.WaitForExitAsync();
                if (metrics.ExitCode != 0) { return metrics.ExitCode; }
            }
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            if (!server.HasExited) { server.Kill(entireProcessTree: true); }
            await server.WaitForExitAsync();
            // Keep this isolated database as experiment evidence. Process exit does not make
            // immediate deletion reliable on Windows; cleanup must not change a passed result.
        }
    }

    private static Process Start(params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, entry) => { if (entry.Data is not null) { Console.WriteLine(entry.Data); } };
        process.ErrorDataReceived += (_, entry) => { if (entry.Data is not null) { Console.Error.WriteLine(entry.Data); } };
        if (!process.Start()) { process.Dispose(); throw new InvalidOperationException("Could not start the probe process."); }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
