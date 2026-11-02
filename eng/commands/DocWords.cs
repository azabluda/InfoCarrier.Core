// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.RegularExpressions;

// Count prose, excluding code, link URLs, HTML, and theme icons. Budgets match docs/doc-style.md.
internal static class DocWords
{
    internal static readonly IReadOnlyDictionary<string, int> Budgets = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["README.md"] = 450,
        ["src/InfoCarrier.Core/PACKAGE.md"] = 350,
        ["src/InfoCarrier.Core.AspNetCore/PACKAGE.md"] = 300,
        ["website/docs/api-surface.md"] = 750,
        ["website/docs/configuration/transports.md"] = 1400,
        ["website/docs/getting-started/upgrading-from-3-1.md"] = 700,
        ["website/docs/release-notes/10.0.md"] = 740,
        ["website/docs/platforms/blazor-webassembly.md"] = 700,
        ["website/docs/release-notes/10.1.md"] = 800,
        ["website/docs/security.md"] = 920,
        ["website/docs/guide/errors.md"] = 950,
        ["website/docs/limitations.md"] = 950,
        ["website/docs/multi-tenancy.md"] = 760,
        ["website/docs/configuration/server.md"] = 1950,
        ["website/docs/guide/querying.md"] = 1300,
        ["website/docs/guide/transactions.md"] = 640,
        ["website/docs/configuration/client.md"] = 1300
    };

    internal static int Prose(string text)
    {
        text = Regex.Replace(text, @"^```.*?^```", " ", RegexOptions.Singleline | RegexOptions.Multiline);
        foreach (var pattern in new[] { @"`[^`]*`", @"<?https?://[^\s>)]+>?", @"<[^>]+>", @":[a-z0-9-]+:", @"[|*#>!\[\]]" })
        {
            // Link targets must go before bare URLs, preserving the visible text.
            if (pattern == @"<?https?://[^\s>)]+>?") text = Regex.Replace(text, @"\]\([^)]*\)", "] ");
            text = Regex.Replace(text, pattern, " ");
        }
        return Regex.Matches(text, @"\S+").Count;
    }

    public static int Run(string[] args)
    {
        try
        {
            var files = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
            if (args.Contains("--all") || files.Length == 0)
                files = ["README.md", "src/InfoCarrier.Core/PACKAGE.md", "src/InfoCarrier.Core.AspNetCore/PACKAGE.md",
                    .. Directory.EnumerateFiles("website/docs", "*.md", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).Order(StringComparer.Ordinal)];
            var over = 0;
            var total = 0;
            foreach (var path in files)
            {
                var key = path.Replace('\\', '/');
                // Preserve the old UTF-8 decoder's treatment of a leading BOM in prose counts.
                var count = Prose(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)));
                total += count;
                var budget = Budgets.GetValueOrDefault(key, 620);
                var flag = count > budget ? $"  OVER by {count - budget} (budget {budget})" : "";
                if (count > budget) over++;
                Console.WriteLine($"{key,-52} {count,4}{flag}");
            }
            if (files.Length > 1) Console.WriteLine($"{"TOTAL",-52} {total,4}  in {files.Length} files, {over} over budget");
            return args.Contains("--budget") && over != 0 ? 1 : 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"doc-words: {error.Message}");
            return 1;
        }
    }
}
