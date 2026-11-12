// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.EngineeringTests;

// A real cross-platform child for argument, output, failure, and cancellation tests.
internal static class ChildProcess
{
    public static async Task<int> Main(string[] args)
    {
        // xUnit v3's VSTest adapter starts this executable with its discovery/execution request.
        // Keep the existing child-process modes for ScriptHost's process tests.
        if (args.Length == 0 || args[0] == "@@")
        {
            return await Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args);
        }

        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        if (args[0] == "wait")
        {
            await File.WriteAllTextAsync(args[1] + ".tmp", Environment.ProcessId.ToString());
            File.Move(args[1] + ".tmp", args[1]);
            // Publish readiness only after Windows has released the rename handle.
            Directory.CreateDirectory(args[1] + ".ready");
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        Console.WriteLine(JsonSerializer.Serialize(args));
        Console.WriteLine("é Ω 漢字 😀");
        Console.Error.WriteLine("child stderr");
        return args[0] == "fail" ? 23 : 0;
    }
}
