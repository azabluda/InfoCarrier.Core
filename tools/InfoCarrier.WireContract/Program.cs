// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.WireContract;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["inspect", var destination, var ledger])
            {
                int[] allocations = JsonSerializer.Deserialize<int[]>(File.ReadAllText(ledger))
                    ?? throw new InvalidOperationException("Missing allocation history.");
                WireContract contract = ContractInspector.Inspect(allocations, await FrameContracts.InspectAsync());
                File.WriteAllText(destination, JsonSerializer.Serialize(contract));
                Console.WriteLine($"Wire {contract.Version}: {contract.Types.Count} types, {contract.Frames.Count} frame cases; runtime {Environment.Version}.");
                return 0;
            }
            if (args is [var mode, var baseline, var candidate, var reference]
                && mode is "compare" or "compare-compatible")
            {
                WireContract Read(string path) => JsonSerializer.Deserialize<WireContract>(File.ReadAllText(path))
                    ?? throw new InvalidOperationException("Missing contract description: " + path);
                ComparisonResult result = ContractComparer.Compare(Read(baseline), Read(candidate), reference);
                foreach (ContractChange change in result.Changes)
                {
                    Console.WriteLine($"{change.Kind}: {change.Path}; direction={change.Direction}; baseline={reference}; "
                        + $"previous={change.Previous}; current={change.Current}; {change.Reason}");
                }
                foreach (string error in result.AllocationErrors)
                {
                    Console.Error.WriteLine($"Allocation failure; baseline={reference}; versions={result.PreviousVersion}->{result.CurrentVersion}; {error}");
                }
                if (result.Changes.Any(c => c.Kind == ChangeKind.Unclassified))
                {
                    Console.Error.WriteLine("Complete coverage or classification. Increasing the version does not resolve incomplete analysis.");
                }
                if (result.Changes.Any(c => c.Kind == ChangeKind.Incompatible))
                {
                    Console.WriteLine("Restore accidental changes or deliberately allocate a fresh version. Address supported clients separately.");
                }
                bool passed = result.Passed && (mode != "compare-compatible"
                    || !result.Changes.Any(c => c.Kind == ChangeKind.Incompatible));
                if (mode == "compare-compatible" && !passed)
                {
                    Console.Error.WriteLine("A fresh version cannot waive compatibility with this release reference.");
                }
                Console.WriteLine($"{(passed ? "PASS" : "FAIL")}: structural gate; reference={reference}; wire={result.PreviousVersion}->{result.CurrentVersion}.");
                return passed ? 0 : 1;
            }
            Console.Error.WriteLine("Usage: inspect <description.json> <allocation-history.json> | compare[-compatible] <baseline.json> <candidate.json> <reference>");
            return 2;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("Incomplete wire analysis: " + exception.Message);
            return 2;
        }
    }
}
