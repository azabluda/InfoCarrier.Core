// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace InfoCarrier.EngineeringTests;

public sealed class WireContractRunnerTest : IDisposable
{
    private const string Head = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Target = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Feature = "cccccccccccccccccccccccccccccccccccccccc";
    private readonly string root = Path.Combine(WireContractCli.Root(), "artifacts", "engineering tests " + Guid.NewGuid().ToString("N"));
    private readonly List<string[]> commands = [];
    private Func<string[], CommandResult>? respond;

    private WireContractRunner Runner => new(root, (args, _, _, token) =>
    {
        token.ThrowIfCancellationRequested();
        commands.Add(args);
        return Task.FromResult(respond?.Invoke(args) ?? Default(args));
    });

    private static CommandResult Default(string[] args) => args[1] switch
    {
        "rev-parse" => new(0, args[^1].Replace("^{commit}", "") switch
        {
            "HEAD" => Head,
            "target" or "origin/main" => Target,
            "feature" => Feature,
            var reference => reference
        }),
        "merge-base" => new(0, ""),
        "show" when args.Length > 3 => new(0, Target + " " + Feature),
        "show" => new(0, Schema().ToJsonString()),
        _ => throw new InvalidOperationException("Unexpected child: " + string.Join(" ", args))
    };

    private static JsonObject PullRequest => JsonNode.Parse("""
        {"pull_request":{"base":{"sha":"target"},"head":{"sha":"feature"}}}
        """)!.AsObject();

    [Fact]
    public async Task Local_default_uses_historical_head_even_if_main_advanced()
    {
        Assert.Equal(Head, await Runner.SelectReference(null, "", new JsonObject(), Head));
        Assert.Equal("HEAD^{commit}", Assert.Single(commands, c => c[1] == "rev-parse")[^1]);
    }

    [Fact]
    public async Task Merge_candidate_uses_target_instead_of_previous_feature_commit()
        => Assert.Equal(Target, await Runner.SelectReference(null, "pull_request", PullRequest, Head));

    [Fact]
    public async Task Ci_cannot_substitute_a_local_baseline()
        => Assert.Contains("overrides", (await Assert.ThrowsAsync<ArgumentException>(() =>
            Runner.SelectReference("HEAD", "pull_request", new JsonObject(), Head))).Message);

    [Theory]
    [InlineData("feature-parent")]
    [InlineData("old-target feature")]
    public async Task Head_only_or_stale_merge_candidate_fails(string parents)
    {
        respond = args => args[1] == "show" ? new(0, parents) : Default(args);
        Assert.Contains("matching base and head", (await Assert.ThrowsAsync<ArgumentException>(() =>
            Runner.SelectReference(null, "pull_request", PullRequest, Head))).Message);
    }

    [Fact]
    public async Task Missing_target_fails_instead_of_comparing_nothing()
    {
        respond = _ => new(128, "missing ref");
        Assert.Contains("missing ref", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Runner.SelectReference(null, "", new JsonObject(), Head))).Message);
    }

    [Fact]
    public async Task Initial_reference_applies_only_before_policy_ancestry()
    {
        respond = args => args[1] == "merge-base" ? new(args[^1] == Target ? 1 : 0, "") : Default(args);
        Assert.Equal(WireContractRunner.Initial, await Runner.SelectReference("target", "", new JsonObject(), Head));
    }

    [Theory]
    [InlineData(true, "lacks the initial")]
    [InlineData(false, "include the target")]
    public async Task Candidate_requires_policy_and_target_ancestry(bool missingPolicy, string message)
    {
        respond = args => args[1] == "merge-base" && args[^1] == Head
            && (missingPolicy ? args[^2] == WireContractRunner.Initial : args[^2] == Target)
            ? new(1, "") : Default(args);
        Assert.Contains(message, (await Assert.ThrowsAsync<ArgumentException>(() =>
            Runner.SelectReference("target", "", new JsonObject(), Head))).Message);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("0000000000000000000000000000000000000000")]
    [InlineData("")]
    public async Task Push_uses_previous_protected_state_or_main_for_new_branch(string before)
    {
        Assert.Equal(Target, await Runner.SelectReference(null, "push", new JsonObject { ["before"] = before }, Head));
        Assert.Equal((before == "target" ? "target" : "origin/main") + "^{commit}", commands[0][^1]);
    }

    [Fact]
    public async Task Rewritten_push_fails()
    {
        respond = args => args[1] == "merge-base" ? new(1, "") : Default(args);
        Assert.Contains("rewrites", (await Assert.ThrowsAsync<ArgumentException>(() =>
            Runner.SelectReference(null, "push", new JsonObject { ["before"] = "target" }, Head))).Message);
    }

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(false, 0, false)]
    [InlineData(true, 1, false)]
    public async Task Merge_group_requires_matching_identity_and_ancestry(bool identityMatches, int ancestry, bool success)
    {
        respond = args => args[1] == "merge-base" ? new(ancestry, "") : Default(args);
        var data = new JsonObject { ["merge_group"] = new JsonObject { ["base_sha"] = "target",
            ["head_sha"] = identityMatches ? Head : Feature } };
        if (success)
            Assert.Equal(Target, await Runner.SelectReference(null, "merge_group", data, Head));
        else
            await Assert.ThrowsAsync<ArgumentException>(() => Runner.SelectReference(null, "merge_group", data, Head));
    }

    [Fact]
    public async Task Ancestry_command_failure_is_not_false_ancestry()
    {
        respond = args => args[1] == "merge-base" ? new(128, "broken git") : Default(args);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner.SelectReference(null, "", new JsonObject(), Head));
    }

    [Fact]
    public async Task Unsupported_event_and_missing_event_fields_fail_closed()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Runner.SelectReference(null, "schedule", new JsonObject(), Head));
        await Assert.ThrowsAsync<ArgumentException>(() => Runner.SelectReference(null, "merge_group", new JsonObject(), Head));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaz")]
    public async Task Commit_requires_exact_lowercase_git_identity(string value)
    {
        respond = _ => new(0, value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner.Commit("target"));
    }

    [Fact]
    public async Task Description_comes_from_protected_git_and_missing_description_has_no_fallback()
    {
        var description = await Runner.ProtectedDescription("protected-reference");
        Assert.True(JsonNode.DeepEquals(Schema(), description));
        Assert.Equal(new[] { "git", "show", "protected-reference:" + WireContractRunner.Description }, commands.Single());
        respond = _ => new(128, "missing description");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner.ProtectedDescription(WireContractRunner.Initial));
    }

    [Theory]
    [InlineData("{\"FormatVersion\":2}")]
    [InlineData("{\"FormatVersion\":1,\"Contract\":{}}")]
    [InlineData("{}")]
    public void Unsupported_or_incomplete_description_fails(string value)
        => Assert.Throws<ArgumentException>(() => WireContractRunner.ReadDescription(value));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Provenance_does_not_mask_or_create_structural_changes(bool schema)
    {
        var saved = schema ? Schema() : Legacy();
        var actual = (JsonObject)saved.DeepClone();
        if (schema) actual["x-infocarrier"]!["inspection"] = new JsonObject { ["Sdk"] = "newer" };
        else actual["Inspection"] = new JsonObject { ["Sdk"] = "newer" };
        WireContractRunner.VerifySaved(saved, actual);
        if (schema) actual["$defs"]!["quantity"]!["type"] = "string";
        else actual["Contract"]!["Types"]!["quantity"] = "string";
        Assert.Contains(schema ? "defs/quantity/type" : "Contract/Types/quantity",
            Assert.Throws<ArgumentException>(() => WireContractRunner.VerifySaved(saved, actual)).Message);
    }

    [Fact]
    public void Schema_requires_coverage_metadata()
    {
        var description = Schema();
        Assert.True(JsonNode.DeepEquals(description, WireContractRunner.ReadDescription(description.ToJsonString())));
        Assert.False(description.ContainsKey("Contract"));
        description.Remove("x-infocarrier");
        Assert.Throws<ArgumentException>(() => WireContractRunner.ReadDescription(description.ToJsonString()));
    }

    [Theory]
    [InlineData("compare")]
    [InlineData("compare-compatible")]
    [InlineData(null)]
    public async Task Recording_waits_for_every_protected_comparison(string? incompatibleMode)
    {
        var saved = Schema();
        var actual = Schema();
        actual["$defs"]!["quantity"]!["type"] = "string";
        var descriptionPath = Path.Combine(root, WireContractRunner.Description);
        Directory.CreateDirectory(Path.GetDirectoryName(descriptionPath)!);
        WireContractRunner.WriteJson(descriptionPath, incompatibleMode is not null ? actual : saved);
        var original = File.ReadAllBytes(descriptionPath);
        var binary = Path.Combine(root, "tools/InfoCarrier.WireContract/bin/Release/net10.0/InfoCarrier.WireContract.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(binary)!);
        File.WriteAllText(binary, "");
        File.WriteAllText(Path.ChangeExtension(binary, ".deps.json"), "{\"libraries\":{\"Dependency/1.0\":{}}}");
        var compared = new List<string>();
        respond = args =>
        {
            if (args[0] == "git") return Default(args);
            if (args[1] == "--version") return new(0, "10.0.201");
            if (args[1] == "build")
            {
                Assert.Equal(2, commands.Count(c => c[0] == "git" && c[1] == "show"));
                return new(0, "");
            }
            if (args[2] == "inspect")
            {
                var inspected = (JsonObject)actual.DeepClone();
                inspected["x-infocarrier"]!["inventory"]!["runtime:System.Text.Json"] = "10.0";
                WireContractRunner.WriteJson(args[3], inspected);
                return new(0, "");
            }
            Assert.Equal(original, File.ReadAllBytes(descriptionPath));
            Assert.True(JsonNode.DeepEquals(saved, JsonNode.Parse(File.ReadAllText(args[3]))));
            compared.Add(args[2]);
            return new(args[2] == incompatibleMode ? 1 : 0, "incompatible protected contract");
        };
        if (incompatibleMode is not null)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Runner.Run(["--record", "--release-ref", "target"]));
            Assert.Equal(original, File.ReadAllBytes(descriptionPath));
        }
        else
        {
            Assert.Equal(0, await Runner.Run(["--record", "--release-ref=target"]));
            WireContractRunner.VerifySaved(actual, WireContractRunner.ReadDescription(File.ReadAllText(descriptionPath)));
        }
        Assert.Equal(incompatibleMode == "compare" ? new[] { "compare" } : ["compare", "compare-compatible"], compared);
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "artifacts")));
    }

    [Fact]
    public async Task Ci_cannot_record()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "event.json");
        File.WriteAllText(path, "{}");
        Assert.Contains("cannot record", (await Assert.ThrowsAsync<ArgumentException>(() =>
            Runner.Run(["--record"], "push", path))).Message);
        Assert.Empty(commands);
    }

    [Fact]
    public async Task Missing_event_file_fails_before_children()
    {
        Directory.CreateDirectory(root);
        await Assert.ThrowsAsync<FileNotFoundException>(() => Runner.Run([], "push", Path.Combine(root, "missing.json")));
        Assert.Empty(commands);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public async Task Event_must_be_an_object(string value)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "event.json");
        File.WriteAllText(path, value);
        await Assert.ThrowsAsync<ArgumentException>(() => Runner.Run([], "push", path));
        Assert.Empty(commands);
    }

    [Fact]
    public async Task Help_and_invalid_arguments_preserve_exit_codes()
    {
        Assert.Equal(0, await Runner.Run(["--help"]));
        Assert.Equal(2, await Runner.Run(["--base-ref"]));
        Assert.Equal(2, await Runner.Run(["--unknown"]));
        Assert.Empty(commands);
    }

    [Fact]
    public async Task Failed_build_cleans_temporary_files_without_recording()
    {
        respond = args => args[0] == "dotnet" ? new(args[1] == "build" ? 7 : 0, "failed build") : Default(args);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Runner.Run(["--record"]));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "artifacts")));
        Assert.False(File.Exists(Path.Combine(root, WireContractRunner.Description)));
    }

    [Fact]
    public async Task Cancelled_build_cleans_temporary_files_without_recording()
    {
        respond = args => args[1] == "build" ? throw new OperationCanceledException()
            : args[0] == "dotnet" ? new(0, "SDK") : Default(args);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Runner.Run(["--record"]));
        Assert.Empty(Directory.GetDirectories(Path.Combine(root, "artifacts")));
        Assert.False(File.Exists(Path.Combine(root, WireContractRunner.Description)));
    }

    [Fact]
    public void Cleanup_rejects_root_and_siblings()
    {
        Directory.CreateDirectory(root);
        Assert.Throws<ArgumentException>(() => Runner.Cleanup(root));
        var sibling = Path.Combine(root, "artifacts", "ordinary");
        Directory.CreateDirectory(sibling);
        Assert.Throws<ArgumentException>(() => Runner.Cleanup(sibling));
    }

    [Fact]
    public void Cleanup_failure_is_visible()
    {
        var path = Path.Combine(root, "artifacts", "wire-contract-missing");
        Assert.ThrowsAny<IOException>(() => Runner.Cleanup(path));
    }

    [Fact]
    public void Json_output_is_sorted_utf8_without_bom_and_uses_lf()
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "description.json");
        WireContractRunner.WriteJson(path, JsonNode.Parse("{\"z\":\"<é>\",\"a\":[2,1]}")!);
        Assert.Equal("{\n  \"a\": [\n    2,\n    1\n  ],\n  \"z\": \"<é>\"\n}\n", File.ReadAllText(path));
        Assert.False(File.ReadAllBytes(path).AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
    }

    [Theory]
    [InlineData("echo", 0)]
    [InlineData("fail", 23)]
    public async Task Child_arguments_output_and_failure_survive_paths_with_spaces(string mode, int exit)
    {
        Directory.CreateDirectory(root);
        string[] args = [mode, "a b", "quote\"", "$(should-not-run); & | < >", "trailing\\"];
        var result = await WireContractRunner.Execute(["dotnet", typeof(ChildProcess).Assembly.Location, .. args], root, true, default);
        Assert.Equal(exit, result.ExitCode);
        Assert.Contains(JsonSerializer.Serialize(args), result.Output);
        Assert.Contains("é Ω 漢字 😀", result.Output);
        Assert.Contains("child stderr", result.Output);
    }

    [Fact]
    public async Task Cancellation_terminates_the_child()
    {
        Directory.CreateDirectory(root);
        var marker = Path.Combine(root, "child.pid");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var running = WireContractRunner.Execute(["dotnet", typeof(ChildProcess).Assembly.Location, "wait", marker], root, true, cancellation.Token);
        int pid;
        try
        {
            while (!Directory.Exists(marker + ".ready"))
                await Task.Delay(20, cancellation.Token);
            pid = int.Parse(await File.ReadAllTextAsync(marker));
        }
        finally
        {
            cancellation.Cancel();
            // Disposal must wait for the child even when reading the readiness file fails.
            try { await running; } catch (OperationCanceledException) { }
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid));
    }

    private static JsonObject Legacy() => JsonNode.Parse("""
        {"FormatVersion":1,"Contract":{"Version":3,"AllocatedVersions":[1,2,3],"Types":{"quantity":"integer"},
        "Frames":{"start":[0]},"Inventory":{"context":"default"}},"Inspection":{"Sdk":"original"}}
        """)!.AsObject();

    private static JsonObject Schema() => JsonNode.Parse("""
        {"$schema":"https://json-schema.org/draft/2020-12/schema","$defs":{"quantity":{"type":"integer"}},
        "x-infocarrier":{"formatVersion":2,"protocolVersion":3,"allocatedVersions":[1,2,3],
        "inventory":{"context":"default"},"inspection":{"Sdk":"original"}}}
        """)!.AsObject();

    public void Dispose()
    {
        // Every instance owns this exact artifacts child; never delete a computed repository path.
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
