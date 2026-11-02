// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.RegularExpressions;

// Publish a clean trimmed client and gate only InfoCarrier's warning count against the baseline.
// Warnings are intentionally enabled and nonfatal: this command measures them rather than hiding them.
internal static class TrimRatchet
{
    private static string Root() => ScriptHost.Root;
    internal static Dictionary<string, int> Counts(string text)
    {
        static string Owner(string diagnostic)
        {
            var member = diagnostic.Split(": ", 2)[0].Split('(')[0];
            foreach (var (prefix, owner) in new[] { ("InfoCarrier.Core.", "InfoCarrier.Core"), ("Castle.", "Castle.Core"),
                ("Microsoft.EntityFrameworkCore", "EFCore"), ("Microsoft.AspNetCore", "AspNetCore"), ("Microsoft.FluentUI", "FluentUI"), ("Northwind.", "Northwind") })
                if (member.StartsWith(prefix, StringComparison.Ordinal)) return owner;
            return "other";
        }
        // Match the declaring member, never the csproj path appended to the diagnostic.
        return Regex.Matches(text, @"(?:Trim analysis warning|warning|error)\s+(IL\d{4}):\s*(.*)$", RegexOptions.Multiline)
            .Select(m => (Code: m.Groups[1].Value, Diagnostic: m.Groups[2].Value.Trim())).Distinct()
            .GroupBy(d => Owner(d.Diagnostic)).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    public static async Task<int> Run(string[] args, CancellationToken token)
    {
        if (args.Length > 1) { Console.Error.WriteLine("usage: TrimRatchet.cs [baseline-file]"); return 2; }
        try
        {
            var root = Root();
            var baselineFile = args.Length == 0 ? Path.Combine(root, "eng/trim-baseline.txt") : args[0];
            var directory = Path.Combine(root, "artifacts/trim");
            Directory.CreateDirectory(directory);
            var log = Path.Combine(directory, "publish.log");
            // Check each absolute target before recursive deletion. A clean publish must run ILLink.
            foreach (var relative in new[] { "samples/Northwind.Client/obj/Release", "samples/Northwind.Client/bin/Release" })
            {
                var target = Path.GetFullPath(Path.Combine(root, relative));
                if (!target.StartsWith(Path.GetFullPath(Path.Combine(root, "samples/Northwind.Client")) + Path.DirectorySeparatorChar,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new IOException("Unsafe cleanup target.");
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
            Console.WriteLine("trim-ratchet: publishing samples/Northwind.Client (Release, trimmed, clean)…");
            var result = await ScriptHost.Execute("dotnet",
                ["publish", Path.Combine(root, "samples/Northwind.Client/Northwind.Client.csproj"), "-c", "Release", "--nologo", "-p:TreatWarningsAsErrors=false"],
                root, true, token, log);
            var text = await File.ReadAllTextAsync(log, token);
            if (result.ExitCode != 0) throw new IOException($"trim-ratchet: the publish itself failed ? see {log}\n"
                + string.Join("\n", text.Split('\n').Where(l => l.Contains(" error ", StringComparison.Ordinal)).Take(10)));
            if (!Regex.IsMatch(text, "Optimizing assemblies for size|ILLink")) throw new IOException($"trim-ratchet: the publish produced no ILLink output — the trimmer did not run.\nA count taken from this log would be meaningless. See {log}");
            var counts = Counts(text);
            var ours = counts.GetValueOrDefault("InfoCarrier.Core");
            Console.WriteLine($"\nOURS: {ours}   TOTAL (all assemblies): {counts.Values.Sum()}\n");
            foreach (var (owner, count) in counts.OrderByDescending(p => p.Value)) Console.WriteLine($"    {count}\t{owner}");
            Console.WriteLine();
            var baseline = Regex.Match(await File.ReadAllTextAsync(baselineFile), @"^ours=(\d+)\r?$", RegexOptions.Multiline);
            if (!baseline.Success) throw new IOException($"trim-ratchet: no 'ours=' line in {baselineFile}");
            var previous = int.Parse(baseline.Groups[1].Value);
            if (ours > previous) throw new IOException($"trim-ratchet: FAIL — InfoCarrier.Core trim warnings rose from {previous} to {ours}.\nLower the baseline in the same commit as a fix; raise it only with a reason.");
            if (ours < previous) Console.WriteLine($"trim-ratchet: improved ({previous} -> {ours}). Lower 'ours=' in {baselineFile}.");
            Console.WriteLine($"trim-ratchet: OK ({ours} <= {previous}).");
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("trim-ratchet: cancelled."); return 130; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { Console.Error.WriteLine(error.Message); return 1; }
    }
}
