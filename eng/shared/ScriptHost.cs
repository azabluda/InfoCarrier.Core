// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

// Shared process boundary for engineering apps: no shell-built argument strings, no abandoned
// children on Ctrl+C, and both redirected pipes drained before a result or cancellation escapes.
internal static class ScriptHost
{
    internal static string Root { get; } = SourceRoot();
    private static string SourceRoot([CallerFilePath] string source = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "../.."));

    internal sealed record ProcessResult(int ExitCode, string Output, string Error);

    internal static async Task<ProcessResult> Execute(string executable, string[] args, string cwd,
        bool capture, CancellationToken token, string? log = null)
    {
        token.ThrowIfCancellationRequested();
        var redirect = capture || log is not null;
        var start = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = redirect, RedirectStandardError = redirect,
            StandardOutputEncoding = redirect ? Encoding.UTF8 : null,
            StandardErrorEncoding = redirect ? Encoding.UTF8 : null };
        foreach (var argument in args) start.ArgumentList.Add(argument);
        using var writer = log is null ? null : new StreamWriter(log, false, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        using var gate = new SemaphoreSlim(1);
        using var child = Process.Start(start) ?? throw new IOException("Cannot start " + executable);
        async Task<string> Drain(StreamReader input)
        {
            if (writer is null) return await input.ReadToEndAsync();
            while (await input.ReadLineAsync() is { } line)
            {
                await gate.WaitAsync();
                try { await writer.WriteLineAsync(line); } finally { gate.Release(); }
            }
            return "";
        }
        var stdout = redirect ? Drain(child.StandardOutput) : Task.FromResult("");
        var stderr = redirect ? Drain(child.StandardError) : Task.FromResult("");
        try { await child.WaitForExitAsync(token); }
        catch (OperationCanceledException)
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        return new(child.ExitCode, await stdout, await stderr);
    }

    // One console cancellation handler per application. Helpers receive the token explicitly.
    internal static async Task<int> RunAsync(Func<CancellationToken, Task<int>> run, string name)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try { return await run(cancellation.Token); }
        catch (OperationCanceledException) { Console.Error.WriteLine(name + ": cancelled."); return 130; }
        finally { Console.CancelKeyPress -= cancel; }
    }

}
