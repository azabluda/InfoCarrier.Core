// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;

namespace InfoCarrier.EngineeringTests;

// Small owned fixtures cover the contracts consumed by CI; real-suite checks complement these.
public sealed class ScriptBehaviorTest : IDisposable
{
    private readonly string directory = Path.Combine(WireContractCli.Root(), "artifacts", "script fixtures " + Guid.NewGuid().ToString("N"));

    private string File(string name, string text)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    [Theory]
    [InlineData("A `method` and [a link](page.md)", "a-method-and-a-link")]
    [InlineData("**Über**: α_β!", "über-αβ")]
    [InlineData("Spaces   and / punctuation?", "spaces-and-punctuation")]
    public void Heading_slugs_preserve_visible_text(string heading, string expected)
        => Assert.Equal(expected, DocLinks.Slug(heading));

    [Fact]
    public void Markdown_links_include_duplicate_anchors_but_ignore_fences_and_external_links()
    {
        var page = File("page.md", "# Title\n# Title\n```\n# Hidden\n```\n");
        var entry = File("entry.md", "[good](page.md#title_1) [bad](page.md#hidden) [missing](missing.md) [web](https://example.test)\n```\n[ignore](gone.md)\n```\n");
        Assert.Equal(new[] { "title", "title_1" }, DocLinks.Anchors(page).Order(StringComparer.Ordinal));
        using var output = new StringWriter();
        Assert.Equal(2, DocLinks.Check([entry], output));
        Assert.Contains("no such heading", output.ToString());
        Assert.Contains("no such file", output.ToString());
        Assert.DoesNotContain("gone.md", output.ToString());
    }

    [Fact]
    public void Word_count_excludes_markup_targets_code_html_and_icons()
        => Assert.Equal(4, DocWords.Prose("One [two](https://example.test) `code here`\n```cs\nignored words\n```\n<three>four</three> :material-icon: https://other.test five"));

    [Fact]
    public void Existing_word_budgets_remain_unchanged()
    {
        Assert.Equal(450, DocWords.Budgets["README.md"]);
        Assert.Equal(1950, DocWords.Budgets["website/docs/configuration/server.md"]);
        Assert.Equal(1400, DocWords.Budgets["website/docs/configuration/transports.md"]);
    }

    private const string Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    [Fact]
    public void Failures_are_nested_unioned_decoded_and_sorted_by_code_point()
    {
        var first = File("first.trx", $"<TestRun xmlns=\"{Ns}\"><Results><UnitTestResult outcome=\"Failed\" testName=\"Z&lt;T>\"><InnerResults><UnitTestResult outcome=\"Failed\" testName=\"A&amp;B\" /></InnerResults></UnitTestResult><UnitTestResult outcome=\"Passed\" testName=\"pass\" /></Results></TestRun>");
        var second = File("second.trx", $"<TestRun xmlns=\"{Ns}\"><UnitTestResult outcome=\"Failed\" testName=\"A&amp;B\" /><UnitTestResult outcome=\"Failed\" testName=\"😀\" /><UnitTestResult outcome=\"Failed\" testName=\"\uE000\" /></TestRun>");
        Assert.Equal(new[] { "A&B", "Z<T>", "\uE000", "😀" }, TrxFailures.Failing([first, second]));
    }

    [Fact]
    public void Counters_are_read_independently_including_skipped_tests_in_total()
    {
        var first = File("first.trx", "<TestRun><Counters total=\"10\" passed=\"6\" failed=\"1\" /></TestRun>");
        var second = File("second.trx", "<TestRun><Counters total=\"4\" passed=\"2\" failed=\"0\" /></TestRun>");
        Assert.Equal((14L, 8L, 1L), SuiteSummary.Counters([first, second]));
    }

    [Theory]
    [InlineData("<TestRun />")]
    [InlineData("<TestRun><Counters total=\"4\" passed=\"3\" /></TestRun>")]
    [InlineData("<TestRun><Counters total=\"no\" passed=\"3\" failed=\"0\" /></TestRun>")]
    public void Missing_or_invalid_counters_fail_instead_of_deriving_a_value(string text)
        => Assert.Throws<InvalidDataException>(() => SuiteSummary.Counters([File("missing.trx", text)]));

    [Fact]
    public void Parity_excludes_controls_and_skips_and_rounds_down_with_case_matching()
    {
        var reasons = File("reasons.tsv", "control\tControl\nreason\tWire\tMethod\tUpdate\tinfocarrier\tINFOCARRIER DEFECT #99\n");
        var trx = File("results.trx", $"""
            <TestRun xmlns="{Ns}"><Results>
            <UnitTestResult testId="1" testName="Method(mode: Update)" outcome="Failed" />
            <UnitTestResult testId="2" testName="Method(mode: Updated)" outcome="Passed" />
            <UnitTestResult testId="3" testName="Method(mode: Read)" outcome="Passed" />
            <UnitTestResult testId="4" testName="Method(mode: Update)" outcome="NotExecuted" />
            <UnitTestResult testId="5" testName="Method(mode: Update)" outcome="Passed" />
            </Results><TestDefinitions>
            <UnitTest id="1"><TestMethod className="Wire" name="Method" /></UnitTest>
            <UnitTest id="2"><TestMethod className="Wire" name="Method" /></UnitTest>
            <UnitTest id="3"><TestMethod className="Wire" name="Method" /></UnitTest>
            <UnitTest id="4"><TestMethod className="Wire" name="Method" /></UnitTest>
            <UnitTest id="5"><TestMethod className="Control" name="Method" /></UnitTest>
            </TestDefinitions></TestRun>
            """);
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(0, SpecParity.Run([reasons, "--", trx], output, error));
        Assert.Equal("parity=66.66\nparity_ran=3\nparity_infocarrier=1\nparity_skipped=1\nparity_control=1\ninfocarrier_defects=1\n", output.ToString());
        Assert.Contains("#99", error.ToString());
    }

    [Theory]
    [InlineData("bad reason line\n", "<TestRun />")]
    [InlineData("", "<TestRun />")]
    [InlineData("", "<TestRun><Results><UnitTestResult testId='1'/></Results></TestRun>")]
    public void Parity_rejects_invalid_or_empty_input(string reasons, string trx)
    {
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(1, SpecParity.Run([File("reasons.tsv", reasons), "--", File("bad.trx", trx)], output, error));
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void Missing_namespaced_definition_fails_closed()
    {
        var path = File("missing.trx", $"<TestRun xmlns=\"{Ns}\"><UnitTestResult testId=\"1\" testName=\"lost\" outcome=\"Passed\" /></TestRun>");
        Assert.Throws<InvalidDataException>(() => SpecParity.ReadResults([path]).ToArray());
    }

    [Fact]
    public void Measurement_reads_final_summary_and_keeps_failures_and_reasons()
    {
        const string log = "[xUnit.net 00:00:01] Example.Case [FAIL]\n  Error Message:\n   Assert.Equal() Failure: expected green\n\n  Stack Trace:\nTotal tests: 12\n     Passed: 9\n     Failed: 2\n     Skipped: 1\n";
        Assert.Equal((2, 12), Measure.Summary(log));
        Assert.Equal(new[] { "Example.Case" }, Measure.FailureNames(log));
        Assert.Equal(new[] { "      1 Assert.Equal() Failure: expected green" }, Measure.Reasons(log));
        Assert.Equal((0, 20), Measure.Summary("Total tests: 20\n     Passed: 20\n"));
        Assert.Throws<InvalidDataException>(() => Measure.Summary("Build failed."));
    }

    [Fact]
    public void Reason_diff_preserves_normal_diff_headers_and_counts()
    {
        Assert.Equal(new[] { "1c1", "< old", "---", "> new" }, Measure.ReasonDiff(["old"], ["new"]));
        Assert.Equal(new[] { "0a1,2", "> a", "> b" }, Measure.ReasonDiff([], ["a", "b"]));
        Assert.Equal(new[] { "2d1", "< b" }, Measure.ReasonDiff(["a", "b"], ["a"]));
    }

    [Fact]
    public void Trim_diagnostics_use_declaring_members_not_repository_paths_and_are_deduplicated()
    {
        var text = "warning IL2026: InfoCarrier.Core.X.Member(): details [C:/InfoCarrier/Northwind.Client.csproj]\n"
            + "warning IL2026: InfoCarrier.Core.X.Member(): details [C:/InfoCarrier/Northwind.Client.csproj]\n"
            + "Trim analysis warning IL2070: Microsoft.EntityFrameworkCore.Type.Member(): details [C:/InfoCarrier/Northwind.Client.csproj]\n"
            + "warning IL2111: Northwind.App.Member(): details\n";
        var counts = TrimRatchet.Counts(text);
        Assert.Equal(1, counts["InfoCarrier.Core"]);
        Assert.Equal(1, counts["EFCore"]);
        Assert.Equal(1, counts["Northwind"]);
        Assert.Equal(3, counts.Values.Sum());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
