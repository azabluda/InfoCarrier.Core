// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.RegularExpressions;

// Validate both the page and heading of local Markdown links. Code fences are not prose links.
internal static class DocLinks
{
    private static readonly Regex Fence = new(@"^```.*?^```", RegexOptions.Multiline | RegexOptions.Singleline);
    internal static string Slug(string text)
    {
        text = Regex.Replace(text, @"`([^`]*)`", "$1");
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, @"[*_]{1,3}", "");
        text = Regex.Replace(text, @"[^\p{L}\p{N}_\- ]", "").Trim().ToLowerInvariant();
        return Regex.Replace(text, @"\s+", "-");
    }

    internal static HashSet<string> Anchors(string path)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(Fence.Replace(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)), ""), @"^#{1,6}\s+(.*?)\s*$", RegexOptions.Multiline))
        {
            var slug = Slug(match.Groups[1].Value);
            seen.TryGetValue(slug, out var count);
            seen[slug] = count + 1;
            anchors.Add(count == 0 ? slug : $"{slug}_{count}");
        }
        return anchors;
    }

    internal static int Check(IEnumerable<string> files, TextWriter output)
    {
        var bad = 0;
        foreach (var path in files)
        {
            var body = Fence.Replace(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)), "");
            foreach (Match match in Regex.Matches(body, "\\[[^\\]]*\\]\\(([^)\\s]+)(?:\\s+\"[^\"]*\")?\\)"))
            {
                var target = match.Groups[1].Value;
                if (new[] { "http://", "https://", "mailto:", "//" }.Any(p => target.StartsWith(p, StringComparison.Ordinal))) continue;
                var parts = target.Split('#', 2);
                var resolved = parts[0].Length == 0 ? path : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path) ?? "", parts[0]));
                string? error = null;
                if (parts[0].Length != 0 && !File.Exists(resolved) && !Directory.Exists(resolved)) error = "no such file";
                else if (parts.Length == 2 && parts[1].Length != 0 && resolved.EndsWith(".md", StringComparison.Ordinal)
                    && !Anchors(resolved).Contains(parts[1])) error = "no such heading";
                if (error is null) continue;
                output.WriteLine($"  {path.Replace('\\', '/')} -> {target}  ({error})");
                bad++;
            }
        }
        return bad;
    }

    private static IEnumerable<string> Markdown(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal) : [];

    public static int Run(string[] args)
    {
        try
        {
            var files = (args.Length != 0 ? args : new[] { "README.md", "CONTRIBUTING.md" }
                .Concat(Markdown("src")).Concat(Markdown("website/docs")).Concat(Markdown("docs")))
                .Where(File.Exists).Where(p => !Path.GetFileName(p).Contains(".superseded-", StringComparison.Ordinal)).ToArray();
            var bad = Check(files, Console.Out);
            Console.WriteLine($"doc-links: {bad} link{(bad == 1 ? "" : "s")} broken in {files.Length} files");
            return bad == 0 ? 0 : 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"doc-links: {error.Message}");
            return 1;
        }
    }
}
