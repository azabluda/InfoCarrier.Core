// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static class CompatibilityCommand
{
    internal static async Task<int> Run(string[] args)
    {
        if (args.Length < 2 || args[0] != "--previous-client")
        {
            Console.Error.WriteLine("Usage: dotnet run --file eng/Compatibility.cs -- --previous-client <unchanged-client.dll> [--historical-client <expanded-client.dll>] [--retired]");
            return 2;
        }
        string previousClient = args[1];
        string? historicalClient = null;
        bool retired = false;
        for (int index = 2; index < args.Length; index++)
        {
            if (args[index] == "--retired") { retired = true; }
            else if (args[index] == "--historical-client" && ++index < args.Length) { historicalClient = Path.GetFullPath(args[index]); }
            else { Console.Error.WriteLine("Unknown or incomplete compatibility option."); return 2; }
        }
        string current = Path.GetFullPath("tools/InfoCarrier.CompatibilityProbe/bin/Release/net10.0/InfoCarrier.CompatibilityProbe.dll");
        previousClient = Path.GetFullPath(previousClient);
        if (!File.Exists(previousClient) || !File.Exists(current) || historicalClient is not null && !File.Exists(historicalClient))
        {
            Console.Error.WriteLine("Build the current probe and supply a preserved, unchanged earlier probe DLL.");
            return 2;
        }
        if (retired)
        {
            using Process inventory = Start(current, "verify-retired");
            await inventory.WaitForExitAsync();
            if (inventory.ExitCode != 0) { return inventory.ExitCode; }
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
            List<(string Client, string Mode, bool Refused)> clients = [(previousClient, "client", retired)];
            if (historicalClient is not null) { clients.Add((historicalClient, "acceptance", retired)); }
            clients.Add((current, "client", false));
            clients.Add((current, "acceptance", false));
            foreach ((string client, string mode, bool refused) in clients)
            {
                Console.WriteLine("Separate client process: " + client);
                var diagnostics = new StringBuilder();
                using Process process = Start(line => diagnostics.AppendLine(line), client, mode, address + "/infocarrier");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); } throw; }
                if (!refused && process.ExitCode != 0) { return process.ExitCode; }
                if (refused)
                {
                    if (process.ExitCode == 0) { throw new InvalidOperationException("A retired client was accepted."); }
                    string diagnostic = diagnostics.ToString();
                    if (!diagnostic.Contains("InfoCarrierProtocolException", StringComparison.Ordinal)
                        || !diagnostic.Contains("client 3, server 4", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("The historical process failed without the expected compatibility diagnostic.");
                    }
                    Console.WriteLine("PASS retired client reported protocol 3 / server 4 refusal");
                    using Process verify = Start(current, "verify-empty", database);
                    await verify.WaitForExitAsync(timeout.Token);
                    if (verify.ExitCode != 0) { return verify.ExitCode; }
                }
            }
            if (retired)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, address + "/infocarrier")
                {
                    Content = new ByteArrayContent("invalid retired graph"u8.ToArray()),
                };
                request.Headers.Add("InfoCarrier-Stream-Operation", "Query");
                request.Headers.Add("InfoCarrier-Protocol-Version", "3");
                using HttpResponseMessage response = await http.SendAsync(request);
                if (response.StatusCode != HttpStatusCode.BadRequest
                    || !response.Headers.TryGetValues("InfoCarrier-Client-Protocol-Version", out IEnumerable<string>? received)
                    || received.Single() != "3")
                {
                    throw new InvalidOperationException("Retired stream did not receive its protocol refusal.");
                }
                Console.WriteLine("PASS retired stream refused before graph decoding");
            }
            using Process final = Start(current, "verify-empty", database);
            await final.WaitForExitAsync();
            if (final.ExitCode != 0) { return final.ExitCode; }
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
        => Start(null, arguments);

    private static Process Start(Action<string>? observe, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, entry) =>
        {
            if (entry.Data is not null) { lock (process) { observe?.Invoke(entry.Data); } Console.WriteLine(entry.Data); }
        };
        process.ErrorDataReceived += (_, entry) =>
        {
            if (entry.Data is not null) { lock (process) { observe?.Invoke(entry.Data); } Console.Error.WriteLine(entry.Data); }
        };
        if (!process.Start()) { process.Dispose(); throw new InvalidOperationException("Could not start the probe process."); }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
