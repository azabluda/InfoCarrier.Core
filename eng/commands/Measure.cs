// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;

// Usage: <label> [baseline-label]. Build once, run each spec project, and preserve one combined
// failure-name/reason snapshot under artifacts/measure. Transport and engineering tests are excluded.
// A test failure is measurement data; a build failure or absent summary is an error.
internal static class Measure
{
    private static string Root() => ScriptHost.Root;
    // Tiers C and D are suspended until their providers support EF Core 11.
    private static readonly string[] Projects = ["InfoCarrier.Core.FunctionalTests"];

    // Stream to disk, not memory: a specification run can write many megabytes. Cancel the tree,
    // await both output pumps, and retain the partial log for diagnosis.
    private static async Task<int> Run(string[] args, string log, CancellationToken token)
        => (await ScriptHost.Execute("dotnet", args, Root(), true, token, log)).ExitCode;

    internal static (int Failed, int Total) Summary(string log)
    {
        var totals = Regex.Matches(log, @"^Total tests: (\d+)", RegexOptions.Multiline);
        if (totals.Count == 0) throw new InvalidDataException("No summary line — the test host probably crashed.");
        var failures = Regex.Matches(log, @"^ *Failed: *(\d+)", RegexOptions.Multiline);
        return (failures.Count == 0 ? 0 : int.Parse(failures[^1].Groups[1].Value), int.Parse(totals[^1].Groups[1].Value));
    }

    internal static string[] FailureNames(string log) => Regex.Matches(log, @"^\[xUnit\.net [^\]]*\] *(.*) \[FAIL\]\r?$", RegexOptions.Multiline)
        .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    internal static string[] Reasons(string log)
    {
        var lines = log.Split('\n').Select(s => s.TrimEnd('\r')).ToArray();
        var selected = new SortedSet<int>();
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].Contains("Error Message:", StringComparison.Ordinal))
                for (var n = i; n <= i + 3 && n < lines.Length; n++) selected.Add(n);
        return selected.Select(i => lines[i]).Where(s => Regex.IsMatch(s, @"^\s+(System|Microsoft|Assert|InfoCarrier|Xunit)"))
            .Select(s => s.TrimStart(' ')).Select(s => s[..Math.Min(120, s.Length)])
            .GroupBy(s => s, StringComparer.Ordinal).Select(g => $"{g.Count(),7} {g.Key}").OrderDescending(StringComparer.Ordinal).ToArray();
    }

    private static void Write(string path, IEnumerable<string> lines) => File.WriteAllText(path, string.Concat(lines.Select(s => s + "\n")), new UTF8Encoding(false));

    // Emit normal diff format, preserving the reason comparison's existing console contract.
    // A longest-common-subsequence table groups additions, removals, and replacements together.
    internal static IEnumerable<string> ReasonDiff(string[] before, string[] after)
    {
        var lcs = new int[before.Length + 1, after.Length + 1];
        for (var i = before.Length - 1; i >= 0; i--)
            for (var j = after.Length - 1; j >= 0; j--)
                lcs[i, j] = before[i] == after[j] ? 1 + lcs[i + 1, j + 1] : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
        static string Range(int first, int last) => first == last ? first.ToString() : $"{first},{last}";
        var a = 0; var b = 0;
        while (a < before.Length || b < after.Length)
        {
            if (a < before.Length && b < after.Length && before[a] == after[b]) { a++; b++; continue; }
            var oldStart = a; var newStart = b;
            while (a < before.Length || b < after.Length)
            {
                if (a < before.Length && b < after.Length && before[a] == after[b]) break;
                if (a < before.Length && (b == after.Length || lcs[a + 1, b] >= lcs[a, b + 1])) a++;
                else b++;
            }
            var added = a == oldStart; var deleted = b == newStart;
            yield return added ? $"{a}a{Range(newStart + 1, b)}" : deleted ? $"{Range(oldStart + 1, a)}d{b}"
                : $"{Range(oldStart + 1, a)}c{Range(newStart + 1, b)}";
            for (var i = oldStart; i < a; i++) yield return "< " + before[i];
            if (!added && !deleted) yield return "---";
            for (var j = newStart; j < b; j++) yield return "> " + after[j];
        }
    }

    public static async Task<int> Run(string[] args, CancellationToken token)
    {
        if (args.Length == 0 || args.Length > 2) { Console.Error.WriteLine("usage: Measure.cs <label> [baseline-label]"); return 2; }
        // Labels are filenames inside artifacts, never paths to another directory.
        if (args.Any(a => a.Length == 0 || a is "." or ".." || a.IndexOfAny(['/', '\\']) >= 0))
        { Console.Error.WriteLine("measure: labels must be filenames."); return 2; }
        try
        {
            var directory = Path.Combine(Root(), "artifacts/measure");
            Directory.CreateDirectory(directory);
            var prefix = Path.Combine(directory, args[0]);
            var buildLog = prefix + ".build.log";
            if (await Run(["build", Path.Combine(Root(), "InfoCarrier.Core.slnx"), "-v", "q", "--nologo"], buildLog, token) != 0)
                throw new IOException($"measure: build failed — see {buildLog}\n" + string.Join("\n", File.ReadLines(buildLog).Where(l => l.Contains(" error ", StringComparison.Ordinal)).Take(5)));
            File.WriteAllText(prefix + ".log", "", new UTF8Encoding(false));
            var failed = 0; var total = 0;
            foreach (var project in Projects)
            {
                var log = prefix + "." + project + ".log";
                await Run(["test", Path.Combine(Root(), "test", project, project + ".csproj"), "--no-build", "-v", "n", "--nologo"], log, token);
                var text = await File.ReadAllTextAsync(log, token);
                var counts = Summary(text);
                failed += counts.Failed; total += counts.Total;
                Console.WriteLine($"  {project}: failing {counts.Failed} of {counts.Total}");
                await File.AppendAllTextAsync(prefix + ".log", text, new UTF8Encoding(false), token);
            }
            var combined = await File.ReadAllTextAsync(prefix + ".log", token);
            var names = FailureNames(combined); var reasons = Reasons(combined);
            Write(prefix + ".txt", names); Write(prefix + ".reasons.txt", reasons);
            Console.WriteLine($"FAILING: {failed}  TOTAL: {total}  ({args[0]})");
            if (args.Length == 1) return 0;
            var before = Path.Combine(directory, args[1]);
            if (!File.Exists(before + ".txt")) throw new IOException($"measure: no snapshot for baseline '{args[1]}' at {before}.txt");
            var previousNames = File.ReadAllLines(before + ".txt");
            Console.WriteLine($"\nFIXED  (in {args[1]}, not in {args[0]}):");
            foreach (var name in previousNames.Except(names, StringComparer.Ordinal).Order(StringComparer.Ordinal)) Console.WriteLine("  " + name);
            Console.WriteLine($"BROKEN (in {args[0]}, not in {args[1]}):");
            foreach (var name in names.Except(previousNames, StringComparer.Ordinal).Order(StringComparer.Ordinal)) Console.WriteLine("  " + name);
            if (File.Exists(before + ".reasons.txt"))
            {
                var previous = File.ReadAllLines(before + ".reasons.txt");
                if (previous.SequenceEqual(reasons)) Console.WriteLine("\nREASONS: unchanged.");
                else
                {
                    // Preserve complete changed reason lines, including their occurrence counts.
                    Console.WriteLine($"\nREASONS changed (-{args[1]} / +{args[0]}):");
                    foreach (var line in ReasonDiff(previous, reasons)) Console.WriteLine("  " + line);
                }
            }
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("measure: cancelled."); return 130; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { Console.Error.WriteLine(error.Message); return 1; }
    }
}
