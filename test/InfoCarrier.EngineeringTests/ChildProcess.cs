// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.EngineeringTests;

// A real cross-platform child for argument, output, failure, and cancellation tests.
internal static class ChildProcess
{
    public static async Task<int> Main(string[] args)
    {
        if (args[0] == "wait")
        {
            await File.WriteAllTextAsync(args[1] + ".tmp", Environment.ProcessId.ToString());
            File.Move(args[1] + ".tmp", args[1]);
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }
        Console.WriteLine(JsonSerializer.Serialize(args));
        Console.Error.WriteLine("child stderr");
        return args[0] == "fail" ? 23 : 0;
    }
}
