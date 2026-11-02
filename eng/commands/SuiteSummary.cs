// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Xml;

// CI reporting only: dotnet test remains the gate. Read all three counters independently.
// Writes counters.env, failures.txt, parity.md beside the first TRX and appends the job summary.
internal static class SuiteSummary
{
    private static string ScriptDirectory() => Path.Combine(ScriptHost.Root, "eng");
    internal static (long Total, long Passed, long Failed) Counters(IEnumerable<string> paths)
    {
        long total = 0, passed = 0, failed = 0;
        foreach (var path in paths)
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var found = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Counters") continue;
                long Read(string name) => long.TryParse(reader.GetAttribute(name), out var number) && number >= 0
                    ? number : throw new InvalidDataException($"suite-summary: could not read '{name}' from '{path}'.");
                total += Read("total"); passed += Read("passed"); failed += Read("failed");
                found = true;
                break;
            }
            if (!found) throw new InvalidDataException($"suite-summary: '{path}' has no <Counters> element; cannot read that run's summary.");
        }
        return (total, passed, failed);
    }

    // Capture streams separately: parity stdout is machine-readable; stderr is its Markdown table.
    private static async Task<(string Output, string Error)> Script(string name, string[] args, CancellationToken token)
    {
        var path = Path.Combine(ScriptDirectory(), name + ".cs");
        // Keep SDK restore/build diagnostics out of counters.env and failures.txt. Build first,
        // then launch without building so only the application's output reaches those files.
        var build = await ScriptHost.Execute("dotnet", ["build", path, "--configuration", "Release", "--nologo", "--verbosity", "quiet", "-p:TreatWarningsAsErrors=true"],
            ScriptHost.Root, true, token);
        if (build.ExitCode != 0) throw new IOException($"{name} build failed ({build.ExitCode}): {build.Output}{build.Error}");
        var result = await ScriptHost.Execute("dotnet",
            ["run", "--file", path, "--configuration", "Release", "--no-build", "--", .. args],
            ScriptHost.Root, true, token);
        if (result.ExitCode != 0) throw new IOException($"{name} failed ({result.ExitCode}): {result.Output}{result.Error}");
        return (result.Output, result.Error);
    }

    public static async Task<int> Run(string[] args, CancellationToken token)
    {
        if (args.Length == 0) { Console.Error.WriteLine("usage: SuiteSummary.cs <results.trx> [more.trx ...]"); return 2; }
        try
        {
            foreach (var path in args)
                if (!File.Exists(path)) throw new IOException($"suite-summary: no TRX at '{path}' — that test run produced no results at all.");
            var (total, passed, failed) = Counters(args);
            Console.WriteLine($"Passed: {passed}, Failed: {failed}, Total: {total}");
            Console.WriteLine($"Read from {args.Length} TRX: {string.Join(" ", args)}");
            var directory = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
            var counters = Path.Combine(directory, "counters.env");
            await File.WriteAllTextAsync(counters, $"total={total}\npassed={passed}\nfailed={failed}\n", new UTF8Encoding(false));
            // Child commands run from the repository; resolve caller-relative paths before crossing
            // that working-directory boundary. Keep the caller's spellings in the human summary.
            var inputPaths = args.Select(Path.GetFullPath).ToArray();
            var failures = (await Script("TrxFailures", inputPaths, token)).Output;
            await File.WriteAllTextAsync(Path.Combine(directory, "failures.txt"), failures, new UTF8Encoding(false));
            var reasons = Directory.GetFiles(directory, "*.override-reasons.tsv").Order(StringComparer.Ordinal).ToArray();
            if (reasons.Length == 0) throw new IOException($"suite-summary: no *.override-reasons.tsv in '{directory}'. The test steps must set INFOCARRIER_OVERRIDE_REASONS to that directory.");
            var parity = await Script("SpecParity", [.. reasons, "--", .. inputPaths], token);
            await File.AppendAllTextAsync(counters, parity.Output, new UTF8Encoding(false));
            await File.WriteAllTextAsync(Path.Combine(directory, "parity.md"), parity.Error, new UTF8Encoding(false));
            Console.Write(parity.Error);
            var names = failures.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(n => n.TrimEnd('\r')).ToArray();
            if (names.Length != 0) Console.WriteLine("\nFAILED:\n" + string.Join("\n", names.Select(n => "  " + n)));
            if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summary)
            {
                var markdown = $"## Spec suite\n\n| Passed | Failed | Total |\n|--:|--:|--:|\n| {passed} | {failed} | {total} |\n\n{parity.Error}\n";
                if (names.Length != 0) markdown += $"<details open><summary><b>Failed</b> ({failed})</summary>\n\n"
                    + string.Join("\n", names.Select(n => "- `" + n + "`")) + "\n\n</details>\n";
                await File.AppendAllTextAsync(summary, markdown, new UTF8Encoding(false));
            }
            return 0;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or XmlException)
        { Console.Error.WriteLine(error.Message); return 1; }
    }
}
