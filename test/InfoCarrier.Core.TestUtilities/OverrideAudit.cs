// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>What <see cref="OverrideAudit.Run" /> found.</summary>
/// <param name="Violations">Every rule an override breaks. Empty when the assembly is sound.</param>
/// <param name="Report">Every override with its label, reference, skip and deviation, as a table.</param>
public sealed record OverrideAuditResult(IReadOnlyList<string> Violations, string Report);

/// <summary>
///     Checks that every override of a specification test says what the store does and where that is
///     shown, and writes the list of them.
/// </summary>
/// <remarks>
///     <para>
///         <b>MICROSOFT'S <c>Check_all_tests_overridden</c> MADE STRICTER.</b> Theirs proves somebody
///         looked at every test. This proves, for every test whose expectation was changed, what the
///         store did and where that is shown, and it produces the audit of how much the suite does
///         not check and why. <c>docs/plans/v10/test-overhaul.md</c> is the reading.
///     </para>
///     <para>
///         <b>An override of a specification test</b> is a public method of the assembly whose base
///         definition lives in an EF Core assembly and carries xUnit's <see cref="FactAttribute" />,
///         which EF's <c>ConditionalFact</c> and <c>ConditionalTheory</c> derive from. A fixture's
///         <c>OnModelCreating</c> is an override too, and is not a test, so it is not audited.
///     </para>
/// </remarks>
public static class OverrideAudit
{
    private static readonly Regex UpstreamLink = new(
        @"^https://github\.com/[^/]+/[^/]+/blob/[0-9a-f]{40}/[^#]+#L\d+(-L\d+)?$",
        RegexOptions.Compiled);

    private static readonly Regex RepositoryIssue = new(
        @"^https://github\.com/azabluda/InfoCarrier\.Core/issues/\d+$",
        RegexOptions.Compiled);

    private static readonly Regex DecisionId = new(@"^ADR-\d{3}$", RegexOptions.Compiled);

    private static readonly Regex DocumentHeading = new(
        @"^(?<path>[\w\-./]+\.md)#(?<anchor>[\w\-]+)$",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownHeading = new(@"^#{1,6}\s+(.*?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MarkdownFence = new(@"^```.*?^```", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

    /// <summary>Audits every override of a specification test in <paramref name="testAssembly" />.</summary>
    /// <param name="testAssembly">The test project's assembly.</param>
    /// <param name="upstreamDefectsPath">The path of <c>docs/upstream-defects.md</c>, whose sections a DEFECT names.</param>
    /// <param name="decisionsPath">The path of <c>docs/decisions.md</c>, whose headings a DESIGN names.</param>
    public static OverrideAuditResult Run(Assembly testAssembly, string upstreamDefectsPath, string decisionsPath)
    {
        string defects = File.ReadAllText(upstreamDefectsPath);
        string decisions = File.ReadAllText(decisionsPath);
        var violations = new List<string>();
        var audited = new List<(string At, OverrideReasonAttribute Reason)>();

        IEnumerable<Type> types = testAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

        foreach (Type type in types)
        {
            bool isControl = type.IsDefined(typeof(WireFreeControlAttribute), inherit: false);

            IEnumerable<MethodInfo> methods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsSpecificationTestOverride)
                .OrderBy(m => m.Name, StringComparer.Ordinal);

            foreach (MethodInfo method in methods)
            {
                string where = $"{type.Name}.{method.Name}";
                OverrideReasonAttribute[] reasons = [.. method.GetCustomAttributes<OverrideReasonAttribute>(inherit: false)];

                if (reasons.Length == 0)
                {
                    violations.Add($"{where}: overrides a specification test and gives no reason.");
                    continue;
                }

                if (reasons.Length > 1
                    && (reasons.Any(r => r.Case is null)
                        || reasons.Select(r => r.Case).Distinct(StringComparer.Ordinal).Count() != reasons.Length))
                {
                    violations.Add($"{where}: carries {reasons.Length} reasons, so each must name a distinct Case.");
                }

                foreach (OverrideReasonAttribute reason in reasons)
                {
                    string at = reason.Case is null ? where : $"{where} [{reason.Case}]";
                    Check(reason, at, isControl, defects, decisions, violations);
                    audited.Add((at, reason));
                }
            }
        }

        return new OverrideAuditResult(violations, Report(audited));
    }

    /// <summary>
    ///     Walks up from the test binaries to the file at <paramref name="relativePath" /> in the
    ///     repository.
    /// </summary>
    public static string FindRepositoryFile(string relativePath)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"'{relativePath}' is not above '{AppContext.BaseDirectory}'.");
    }

    /// <remarks>
    ///     <b>An abstract test is not an override of an expectation</b>, because the base has none:
    ///     every provider must write the body, as <c>UpdatesRelationalTestBase.Identifiers_are_generated_correctly</c>
    ///     requires. There is nothing to change and so nothing to give a reason for.
    /// </remarks>
    private static bool IsSpecificationTestOverride(MethodInfo method)
    {
        MethodInfo definition = method.GetBaseDefinition();

        return definition.DeclaringType != method.DeclaringType
            && !definition.IsAbstract
            && definition.DeclaringType?.Assembly.GetName().Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
            && definition.IsDefined(typeof(FactAttribute), inherit: true);
    }

    /// <summary>
    ///     A decision is an <c>ADR-nnn</c> heading of <c>docs/decisions.md</c>, or a
    ///     <c>path.md#anchor</c> naming a heading of another document, for a decision recorded
    ///     where it was made rather than in the ADR log.
    /// </summary>
    private static bool DecisionExists(string decision, string decisions)
    {
        if (DecisionId.IsMatch(decision))
        {
            return decisions.Contains($"\n## {decision} ", StringComparison.Ordinal);
        }

        Match heading = DocumentHeading.Match(decision);
        if (!heading.Success)
        {
            return false;
        }

        string document;
        try
        {
            document = File.ReadAllText(FindRepositoryFile(heading.Groups["path"].Value));
        }
        catch (FileNotFoundException)
        {
            return false;
        }

        return MarkdownHeading.Matches(MarkdownFence.Replace(document, string.Empty))
            .Any(m => Slug(m.Groups[1].Value) == heading.Groups["anchor"].Value);
    }

    /// <summary>The anchor Python-Markdown gives a heading, as <c>eng/doc-links.py</c> computes it.</summary>
    private static string Slug(string heading)
    {
        string text = Regex.Replace(heading, "`([^`]*)`", "$1");
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, "[*_]{1,3}", string.Empty);
        text = Regex.Replace(text, @"[^\w\- ]", string.Empty).Trim().ToLowerInvariant();
        return Regex.Replace(text, @"\s+", "-");
    }

    private static void Check(
        OverrideReasonAttribute reason,
        string at,
        bool isControl,
        string defects,
        string decisions,
        List<string> violations)
    {
        switch (reason)
        {
            case InfoCarrierDefectAttribute own:
                if (isControl)
                {
                    violations.Add($"{at}: a wire-free control has no InfoCarrier in it, so it has no InfoCarrier defect.");
                }

                if (!RepositoryIssue.IsMatch(own.Issue))
                {
                    violations.Add($"{at}: '{own.Issue}' is not an issue of this repository.");
                }

                break;

            case InfoCarrierDesignAttribute design:
                if (isControl)
                {
                    violations.Add($"{at}: a wire-free control has no InfoCarrier in it, so it has no InfoCarrier design.");
                }

                if (!DecisionExists(design.Decision, decisions))
                {
                    violations.Add(
                        $"{at}: '{design.Decision}' is neither an ADR heading of docs/decisions.md nor a heading of a repository document.");
                }

                if (string.IsNullOrWhiteSpace(design.Justification))
                {
                    violations.Add($"{at}: names a decision and not which part of it applies.");
                }

                if (design.UpstreamTest is not null && !UpstreamLink.IsMatch(design.UpstreamTest))
                {
                    violations.Add($"{at}: '{design.UpstreamTest}' is not a link with a 40-character commit and a line anchor.");
                }

                if (design.Skip && design.UpstreamTest is null)
                {
                    violations.Add($"{at}: a skip needs an upstream reference, because only upstream's own choice justifies asserting nothing.");
                }

                break;

            case StoreBehaviourAttribute store:
                bool upstream = store.UpstreamTest is not null;
                bool control = store.ControlType is not null;

                if (isControl)
                {
                    if (upstream || control)
                    {
                        violations.Add($"{at}: a wire-free control is the evidence, so it names no reference.");
                    }

                    if (store.Skip)
                    {
                        violations.Add($"{at}: a wire-free control that skips documents nothing.");
                    }
                }
                else
                {
                    if (!upstream && !control)
                    {
                        violations.Add($"{at}: says what the store does and not where that is shown.");
                    }

                    if (store.Skip && !upstream)
                    {
                        violations.Add($"{at}: a skip needs an upstream reference, because only upstream's own choice justifies asserting nothing.");
                    }
                }

                if (upstream)
                {
                    if (!UpstreamLink.IsMatch(store.UpstreamTest!))
                    {
                        violations.Add($"{at}: '{store.UpstreamTest}' is not a link with a 40-character commit and a line anchor.");
                    }

                    if (string.IsNullOrWhiteSpace(store.Justification))
                    {
                        violations.Add($"{at}: copies no justification from upstream. Use Upstream.GaveNoReason when upstream gives none.");
                    }
                }

                if (control)
                {
                    CheckControlReference(store, at, violations);
                }

                if (store is StoreDefectAttribute defect && !defects.Contains($"\n### {defect.Section} ", StringComparison.Ordinal))
                {
                    violations.Add($"{at}: docs/upstream-defects.md has no section {defect.Section}.");
                }

                if (store is StoreIssueAttribute issue && string.IsNullOrWhiteSpace(issue.Key))
                {
                    violations.Add($"{at}: an ISSUE needs a tracker key.");
                }

                break;
        }
    }

    /// <summary>
    ///     A self-hosted reference must name a control test that documents the SAME label for the
    ///     same case, so the wire and the control cannot disagree about what the store does.
    /// </summary>
    private static void CheckControlReference(StoreBehaviourAttribute store, string at, List<string> violations)
    {
        Type target = store.ControlType!;

        if (!target.IsDefined(typeof(WireFreeControlAttribute), inherit: false))
        {
            violations.Add($"{at}: {target.Name} is not a wire-free control.");
            return;
        }

        MethodInfo? evidence = target.GetMethod(
            store.ControlTest!,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        if (evidence is null)
        {
            violations.Add($"{at}: {target.Name} does not override {store.ControlTest}, so it documents nothing about it.");
            return;
        }

        bool agrees = evidence.GetCustomAttributes<StoreBehaviourAttribute>(inherit: false)
            .Any(e => e.GetType() == store.GetType()
                && (store.Case is null ? e.Case is null : e.Case is null || e.Case == store.Case)
                && SameDetail(e, store));

        if (!agrees)
        {
            string forCase = store.Case is null ? string.Empty : $" for case {store.Case}";
            violations.Add($"{at}: {target.Name}.{store.ControlTest} does not document the same label{forCase}.");
        }
    }

    private static bool SameDetail(StoreBehaviourAttribute a, StoreBehaviourAttribute b)
        => (a, b) switch
        {
            (StoreDefectAttribute x, StoreDefectAttribute y) => x.Section == y.Section,
            (StoreIssueAttribute x, StoreIssueAttribute y) => x.Key == y.Key,
            _ => true,
        };

    private static string Report(List<(string At, OverrideReasonAttribute Reason)> audited)
    {
        var text = new StringBuilder();
        int limits = audited.Count(a => a.Reason is StoreLimitAttribute);
        int defects = audited.Count(a => a.Reason is StoreDefectAttribute);
        int issues = audited.Count(a => a.Reason is StoreIssueAttribute);
        int own = audited.Count(a => a.Reason is InfoCarrierDefectAttribute);
        int designs = audited.Count(a => a.Reason is InfoCarrierDesignAttribute);
        int skips = audited.Count(a => IsSkip(a.Reason));
        int deviations = audited.Count(a => !string.IsNullOrWhiteSpace(a.Reason.Deviation));
        int silent = audited.Count(a => a.Reason is StoreBehaviourAttribute { Justification: Upstream.GaveNoReason });

        text.AppendLine(
            $"Override audit: {audited.Count} reasons. LIMIT {limits}, DEFECT {defects}, ISSUE {issues}, "
            + $"INFOCARRIER DEFECT {own}, DESIGN {designs}. Skips {skips}. Deviations {deviations}. "
            + $"Upstream gave no reason {silent}.");
        text.AppendLine("| Override | Label | Reference | Skip | Deviation |");
        text.AppendLine("|---|---|---|---|---|");

        foreach ((string at, OverrideReasonAttribute reason) in audited)
        {
            text.AppendLine($"| {at} | {Label(reason)} | {Reference(reason)} | {Skip(reason)} | {reason.Deviation} |");
        }

        return text.ToString();
    }

    private static string Label(OverrideReasonAttribute reason)
        => reason switch
        {
            StoreLimitAttribute => "LIMIT",
            StoreDefectAttribute d => $"DEFECT {d.Section}",
            StoreIssueAttribute i => $"ISSUE {i.Key}",
            InfoCarrierDefectAttribute => "INFOCARRIER DEFECT",
            InfoCarrierDesignAttribute d => $"DESIGN {d.Decision}",
            _ => "?",
        };

    private static string Reference(OverrideReasonAttribute reason)
        => reason switch
        {
            InfoCarrierDefectAttribute own => own.Issue,
            InfoCarrierDesignAttribute { UpstreamTest: { } link } => link,
            InfoCarrierDesignAttribute d when DecisionId.IsMatch(d.Decision) => $"docs/decisions.md {d.Decision}",
            InfoCarrierDesignAttribute d => d.Decision,
            StoreBehaviourAttribute { UpstreamTest: { } link } => link,
            StoreBehaviourAttribute { ControlType: { } type } store => $"{type.Name}.{store.ControlTest}",
            _ => "(this control)",
        };

    private static bool IsSkip(OverrideReasonAttribute reason)
        => reason is StoreBehaviourAttribute { Skip: true } or InfoCarrierDesignAttribute { Skip: true };

    private static string Skip(OverrideReasonAttribute reason)
        => IsSkip(reason) ? "yes" : "no";
}
