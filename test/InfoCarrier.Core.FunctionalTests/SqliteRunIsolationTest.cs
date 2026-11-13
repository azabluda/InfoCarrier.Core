// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Text.Json;

namespace InfoCarrier.Core.FunctionalTests;

public class SqliteRunIsolationTest
{
    [Fact]
    public async Task Unshared_stores_with_long_fixture_names_remain_independent()
    {
        var (firstPath, secondPath, firstMarker, secondMarker) = await SqliteIsolationChild.LongNames();
        Assert.NotEqual(firstPath, secondPath);
        Assert.Equal("first", firstMarker);
        Assert.Equal("second", secondMarker);
    }

    [Fact]
    public async Task Concurrent_processes_preserve_closed_databases_and_reclaim_only_abandoned_runs()
    {
        string working = Path.Combine(AppContext.BaseDirectory, "isolation-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(working);
        try
        {
            await using var first = Start(working, "first");
            string[] firstPaths = JsonSerializer.Deserialize<string[]>(await first.Read())!;
            // The Open store has uncheckpointed committed data while Closed has no open handles.
            Assert.True(File.Exists(firstPaths[1] + "-wal"));
            Assert.True(File.Exists(firstPaths[1] + "-shm"));

            await using var second = Start(working, "second");
            string[] secondPaths = JsonSerializer.Deserialize<string[]>(await second.Read())!;
            await first.Check();
            await second.Check();
            Assert.NotEqual(Path.GetDirectoryName(firstPaths[0]), Path.GetDirectoryName(secondPaths[0]));
            Assert.Equal(Path.GetDirectoryName(firstPaths[0]), Path.GetDirectoryName(firstPaths[1]));

            await second.Crash();
            string abandoned = Path.GetDirectoryName(secondPaths[0])!;
            File.WriteAllText(secondPaths[0] + "-wal", "abandoned journal");
            File.WriteAllText(secondPaths[0] + "-shm", "abandoned shared memory");
            string root = Path.GetDirectoryName(abandoned)!;
            string partial = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(partial);
            string orphan = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N") + ".lease");
            File.WriteAllText(orphan, "");
            // An unrelated legacy file must not be swept by a new run.
            string unrelated = Path.Combine(working, "unrelated.db");
            File.WriteAllText(unrelated, "retain");
            await using var third = Start(working, "third");
            await using var fourth = Start(working, "fourth");
            string[] thirdPaths = JsonSerializer.Deserialize<string[]>(await third.Read())!;
            string[] fourthPaths = JsonSerializer.Deserialize<string[]>(await fourth.Read())!;
            Assert.NotEqual(Path.GetDirectoryName(thirdPaths[0]), Path.GetDirectoryName(fourthPaths[0]));
            Assert.False(Directory.Exists(abandoned));
            Assert.False(Directory.Exists(partial));
            Assert.False(File.Exists(orphan));
            Assert.Equal("retain", File.ReadAllText(unrelated));
            await first.Check();
            await third.Check();
            await fourth.Check();
        }
        finally
        {
            // This directory was created exclusively by this test, below the test output.
            Directory.Delete(working, recursive: true);
        }
    }

    private static Child Start(string working, string marker)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = working,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add(typeof(SqliteRunIsolationTest).Assembly.Location);
        start.ArgumentList.Add("--sqlite-isolation-child");
        start.ArgumentList.Add(marker);
        return new Child(Process.Start(start)!);
    }

    private sealed class Child(Process process) : IAsyncDisposable
    {
        private readonly Process _process = process;
        private readonly Task<string> _errors = process.StandardError.ReadToEndAsync();

        public async Task<string> Read()
        {
            string? line = await _process.StandardOutput.ReadLineAsync().WaitAsync(
                TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
            Assert.True(line is not null, "Child exited: " + (line is null ? await _errors : ""));
            return line!;
        }

        public async Task Check()
        {
            await _process.StandardInput.WriteLineAsync("check");
            await _process.StandardInput.FlushAsync();
            Assert.Equal("unchanged", await Read());
        }

        public async Task Crash()
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
            _process.Dispose();
        }
    }
}
