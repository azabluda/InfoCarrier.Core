// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

// Only the candidate is built. Protected descriptions always come from Git, never the candidate file.
internal static class WireContractCli
{
    internal static string Root() => SourceRoot();

    private static string SourceRoot([CallerFilePath] string source = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, ".."));

    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            return await new WireContractRunner(Root()).Run(args,
                Environment.GetEnvironmentVariable("GITHUB_EVENT_NAME") ?? "",
                Environment.GetEnvironmentVariable("GITHUB_EVENT_PATH"), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Wire contract check cancelled.");
            return 130;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or JsonException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"Wire contract check failed: {error.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }
}

internal sealed record CommandResult(int ExitCode, string Output);

internal sealed class WireContractRunner(string root,
    Func<string[], string, bool, CancellationToken, Task<CommandResult>>? execute = null)
{
    internal const string Initial = "e877418b42b8991f0d3dc9be9cdfdea794fe3d81";
    internal const string Description = "docs/wire-contracts/current.json";
    internal const string Ledger = "docs/wire-versions.json";
    private const string Tool = "tools/InfoCarrier.WireContract";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ArgumentList preserves spaces and metacharacters without a shell. Drain both pipes concurrently.
    internal static async Task<CommandResult> Execute(string[] args, string cwd, bool capture, CancellationToken token)
    {
        var start = new ProcessStartInfo(args[0]) { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = capture, RedirectStandardError = capture };
        foreach (var argument in args.Skip(1))
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start child process.");
        var stdout = capture ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var stderr = capture ? process.StandardError.ReadToEndAsync() : Task.FromResult("");
        try
        {
            await process.WaitForExitAsync(token);
        }
        catch (OperationCanceledException)
        {
            // A cancelled build must not leave its inspector or build workers running.
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
            throw;
        }
        return new(process.ExitCode, ((await stdout) + (await stderr)).Trim());
    }

    private Task<CommandResult> Invoke(string[] args, bool capture, CancellationToken token, string? cwd = null)
        => (execute ?? Execute)(args, cwd ?? root, capture, token);

    private async Task<string> Command(string[] args, CancellationToken token, bool capture = true, string? cwd = null)
    {
        var result = await Invoke(args, capture, token, cwd);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Command failed ({result.ExitCode}): "
                + new JsonArray(args.Select(arg => (JsonNode?)JsonValue.Create(arg)).ToArray()).ToJsonString()
                + $"\n{result.Output}");
        return result.Output.Trim();
    }

    internal async Task<string> Commit(string reference, CancellationToken token = default)
    {
        var value = await Command(["git", "rev-parse", "--verify", "--end-of-options", reference + "^{commit}"], token);
        if (value.Length != 40 || value.Any(c => !"0123456789abcdef".Contains(c)))
            throw new InvalidOperationException("Reference did not resolve to an exact Git commit.");
        return value;
    }

    private async Task<bool> Ancestor(string older, string newer, CancellationToken token)
    {
        var result = await Invoke(["git", "merge-base", "--is-ancestor", older, newer], true, token);
        if (result.ExitCode is not (0 or 1))
            throw new InvalidOperationException("Cannot establish baseline ancestry.");
        return result.ExitCode == 0;
    }

    internal async Task<string> SelectReference(string? baseRef, string eventName, JsonNode eventData, string head,
        CancellationToken token = default)
    {
        string target;
        if (eventName.Length != 0)
        {
            if (baseRef is not null)
                throw new ArgumentException("CI baseline overrides are forbidden.");
            switch (eventName)
            {
                case "pull_request":
                    var request = eventData["pull_request"];
                    target = await Commit(Text(request?["base"]?["sha"]), token);
                    var proposed = await Commit(Text(request?["head"]?["sha"]), token);
                    var parents = (await Command(["git", "show", "-s", "--format=%P", head], token))
                        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (!parents.SequenceEqual(new[] { target, proposed }))
                        throw new ArgumentException("Run the check on the current pull-request merge candidate, with matching base and head.");
                    break;
                case "merge_group":
                    target = await Commit(Text(eventData["merge_group"]?["base_sha"]), token);
                    if (await Commit(Text(eventData["merge_group"]?["head_sha"]), token) != head
                        || !await Ancestor(target, head, token))
                        throw new ArgumentException("Merge-group identity or ancestry does not match the checked candidate.");
                    break;
                case "push":
                    var before = eventData["before"]?.GetValue<string>() ?? "";
                    target = await Commit(before.Length != 0 && before.Any(c => c != '0') ? before : "origin/main", token);
                    if (!await Ancestor(target, head, token))
                        throw new ArgumentException("Push rewrites protected history; baseline selection needs review.");
                    break;
                default:
                    throw new ArgumentException($"Unsupported CI event: {eventName}");
            }
        }
        else
            target = await Commit(string.IsNullOrEmpty(baseRef) ? "HEAD" : baseRef, token);
        if (!await Ancestor(Initial, head, token))
            throw new ArgumentException("The candidate lacks the initial description revision. Do not compare the earlier versioning implementation.");
        var reference = await Ancestor(Initial, target, token) ? target : Initial;
        if (!await Ancestor(reference, head, token))
            throw new ArgumentException("Update the candidate to include the target branch before checking its contract.");
        return reference;
    }

    private static string Text(JsonNode? value)
        => value?.GetValue<string>() ?? throw new ArgumentException("Missing required event field.");

    internal static JsonObject ReadDescription(string text)
    {
        var description = JsonNode.Parse(text) as JsonObject ?? throw new ArgumentException("Unsupported description format.");
        bool complete;
        if (description["FormatVersion"]?.ToJsonString() == "1")
            complete = new[] { "Types", "Frames", "Inventory", "AllocatedVersions" }
                .All(name => Nonempty(description["Contract"]?[name]));
        else if (description["$schema"]?.GetValue<string>() == "https://json-schema.org/draft/2020-12/schema"
            && description["x-infocarrier"]?["formatVersion"]?.ToJsonString() == "2")
            complete = Nonempty(description["$defs"]) && new[] { "inventory", "allocatedVersions", "protocolVersion" }
                .All(name => Nonempty(description["x-infocarrier"]?[name]));
        else
            throw new ArgumentException("Unsupported description format. Extend coverage explicitly; do not regenerate the reference.");
        if (!complete)
            throw new ArgumentException("Missing or empty contract inventory in committed description.");
        return description;
    }

    private static bool Nonempty(JsonNode? value) => value switch
    {
        null => false,
        JsonObject obj => obj.Count != 0,
        JsonArray array => array.Count != 0,
        _ => value.ToJsonString() is not ("0" or "false" or "\"\"")
    };

    internal async Task<JsonObject> ProtectedDescription(string reference, CancellationToken token = default)
        => ReadDescription(await Command(["git", "show", $"{reference}:{Description}"], token));

    // Ignore provenance only. Report the first structural difference in sorted property order.
    internal static void VerifySaved(JsonObject saved, JsonObject actual)
    {
        static JsonObject Structural(JsonObject document)
        {
            var copy = (JsonObject)document.DeepClone();
            copy.Remove("Inspection");
            (copy["x-infocarrier"] as JsonObject)?.Remove("inspection");
            return copy;
        }
        var found = Difference(Structural(saved), Structural(actual), "description");
        if (found is not null)
            throw new ArgumentException($"Saved candidate description is stale at {found}. "
                + "Run --record after resolving compatibility failures, then review the description diff.");
    }

    private static string? Difference(JsonNode? saved, JsonNode? actual, string path)
    {
        if (saved is JsonObject left && actual is JsonObject right)
        {
            foreach (var key in left.Select(p => p.Key).Union(right.Select(p => p.Key)).Order(StringComparer.Ordinal))
            {
                if (!left.ContainsKey(key) || !right.ContainsKey(key))
                    return $"{path}/{key}: saved={Value(left, key)}; actual={Value(right, key)}";
                var found = Difference(left[key], right[key], path + "/" + key);
                if (found is not null)
                    return found;
            }
            return null;
        }
        return JsonNode.DeepEquals(saved, actual) ? null : $"{path}: saved={saved}; actual={actual}";
    }

    private static string Value(JsonObject obj, string key) => obj.ContainsKey(key) ? obj[key]?.ToJsonString() ?? "null" : "<absent>";

    internal static void WriteJson(string path, JsonNode value)
    {
        static JsonNode? Sorted(JsonNode? node) => node switch
        {
            JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => KeyValuePair.Create(p.Key, Sorted(p.Value)))),
            JsonArray array => new JsonArray(array.Select(Sorted).ToArray()),
            _ => node?.DeepClone()
        };
        File.WriteAllText(path, Sorted(value)!.ToJsonString(JsonOptions).Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
    }

    private async Task<(string Executable, JsonObject Description)> Inspect(string output, CancellationToken token)
    {
        var sdk = await Command(["dotnet", "--version"], token);
        Console.WriteLine($"Inspecting {root}; SDK={sdk}");
        await Command(["dotnet", "build", Path.Combine(root, Tool, "InfoCarrier.WireContract.csproj"),
            "--configuration", "Release"], token, capture: false);
        var binaries = Directory.GetDirectories(Path.Combine(root, Tool, "bin/Release"))
            .Select(dir => Path.Combine(dir, "InfoCarrier.WireContract.dll")).Where(File.Exists).ToArray();
        if (binaries.Length != 1)
            throw new ArgumentException("Expected exactly one built inspector.");
        var executable = binaries[0];
        var dependencies = JsonNode.Parse(await File.ReadAllTextAsync(Path.ChangeExtension(executable, ".deps.json"), token))!;
        var libraries = dependencies["libraries"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        Console.WriteLine("Resolved inputs: " + string.Join(", ", libraries));
        await Command(["dotnet", executable, "inspect", output, Path.Combine(root, Ledger)], token, capture: false);
        var description = ReadDescription(await File.ReadAllTextAsync(output, token));
        var metadata = description["x-infocarrier"]!.AsObject();
        var inventory = metadata["inventory"]!.AsObject();
        var runtime = inventory["runtime:System.Text.Json"]?.DeepClone()
            ?? throw new ArgumentException("Missing runtime inspection input.");
        inventory.Remove("runtime:System.Text.Json");
        metadata["inspection"] = new JsonObject { ["Sdk"] = sdk, ["SystemTextJson"] = runtime,
            ["Dependencies"] = new JsonArray(libraries.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()) };
        WriteJson(output, description);
        return (executable, description);
    }

    internal void Cleanup(string path)
    {
        var resolved = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var artifacts = Path.GetFullPath(Path.Combine(root, "artifacts"));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetDirectoryName(resolved), artifacts, comparison)
            || !Path.GetFileName(resolved).StartsWith("wire-contract-", StringComparison.Ordinal)
            || (File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("Refusing recursive cleanup outside the temporary comparison directory.");
        Directory.Delete(resolved, recursive: true);
    }

    internal async Task<int> Run(string[] args, string eventName = "", string? eventPath = null, CancellationToken token = default)
    {
        string? baseRef = null;
        var record = false;
        var releases = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    Console.WriteLine("Check actual wire metadata against committed descriptions from protected Git revisions.\n"
                        + "Usage: dotnet run --file eng/wire-contract.cs -- [--base-ref target] [--record] [--release-ref revision ...]\n"
                        + "--base-ref: local target; unavailable in CI.\n--record: record only after protected comparisons pass.\n"
                        + "--release-ref: require compatibility with a release; repeat for each revision.");
                    return 0;
                case "--record": record = true; break;
                case "--base-ref" when i + 1 < args.Length && !args[i + 1].StartsWith('-'): baseRef = args[++i]; break;
                case "--release-ref" when i + 1 < args.Length && !args[i + 1].StartsWith('-'): releases.Add(args[++i]); break;
                default:
                    if (args[i].StartsWith("--base-ref=", StringComparison.Ordinal)) baseRef = args[i][11..];
                    else if (args[i].StartsWith("--release-ref=", StringComparison.Ordinal)) releases.Add(args[i][14..]);
                    else
                    {
                        Console.Error.WriteLine($"Unrecognized or incomplete argument: {args[i]}");
                        return 2;
                    }
                    break;
            }
        }
        var eventData = eventName.Length != 0
            ? JsonNode.Parse(await File.ReadAllTextAsync(eventPath ?? throw new ArgumentException("Missing GITHUB_EVENT_PATH."), token)) as JsonObject
                ?? throw new ArgumentException("Expected a GitHub event object.")
            : new JsonObject();
        if (eventName.Length != 0 && record)
            throw new ArgumentException("CI cannot record descriptions. Commit a reviewed local update.");
        var reference = await SelectReference(baseRef, eventName, eventData, await Commit("HEAD", token), token);
        Console.WriteLine($"Protected structural reference: {reference}");
        Directory.CreateDirectory(Path.Combine(root, "artifacts"));
        var temporary = Path.Combine(root, "artifacts", "wire-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            var baseline = Path.Combine(temporary, "baseline.json");
            var candidate = Path.Combine(temporary, "candidate.json");
            var references = new List<(string Ref, string Mode)> { (reference, "compare") };
            foreach (var release in releases)
                references.Add((await Commit(release, token), "compare-compatible"));
            // Resolve every protected input before building or updating the candidate.
            var descriptions = new List<(string Ref, string Mode, JsonObject Document)>();
            foreach (var (revision, mode) in references)
                descriptions.Add((revision, mode, await ProtectedDescription(revision, token)));
            var (executable, actual) = await Inspect(candidate, token);
            foreach (var (revision, mode, document) in descriptions)
            {
                var inputs = document["Inspection"] ?? document["x-infocarrier"]?["inspection"] ?? new JsonObject();
                Console.WriteLine($"Reference inspection inputs ({revision}): {inputs.ToJsonString()}");
                WriteJson(baseline, document);
                await Command(["dotnet", executable, mode, baseline, candidate, revision], token, capture: false);
            }
            if (record)
            {
                WriteJson(Path.Combine(root, Description), actual);
                Console.WriteLine($"Recorded {Description}. Review and commit its diff; protected comparisons passed.");
            }
            else
            {
                VerifySaved(ReadDescription(await File.ReadAllTextAsync(Path.Combine(root, Description), token)), actual);
                Console.WriteLine("Saved candidate description matches the inspected implementation.");
            }
            return 0;
        }
        finally
        {
            Cleanup(temporary);
        }
    }
}
