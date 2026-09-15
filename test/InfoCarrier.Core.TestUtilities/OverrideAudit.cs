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
///         definition lives in an EF Core assembly, is not abstract, and carries xUnit's
///         <see cref="FactAttribute" />, which EF's <c>ConditionalFact</c> and <c>ConditionalTheory</c>
///         derive from. A fixture's <c>OnModelCreating</c> is an override too, and is not a test.
///     </para>
///     <para>
///         <b>An upstream reference is checked against the checkout it names, where one exists.</b>
///         Its lines must declare the overriding test, by name, at the pinned commit. <c>subrepos/</c>
///         is git-ignored, so a CI runner has no checkout and the report says how many references it
///         could not check; a local run checks all of them.
///     </para>
/// </remarks>
public static class OverrideAudit
{
    private static readonly Regex MarkdownHeading = new(@"^#{1,6}\s+(.*?)\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex MarkdownFence = new(@"^```.*?^```", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

    /// <summary>Audits every override of a specification test in <paramref name="testAssembly" />.</summary>
    /// <param name="testAssembly">The test project's assembly.</param>
    /// <param name="upstreamDefectsPath">The path of <c>docs/upstream-defects.md</c>, whose sections a DEFECT names.</param>
    /// <param name="decisionsPath">The path of <c>docs/decisions.md</c>, whose headings a DESIGN names.</param>
    public static OverrideAuditResult Run(Assembly testAssembly, string upstreamDefectsPath, string decisionsPath)
    {
        var audit = new Audit(File.ReadAllText(upstreamDefectsPath), File.ReadAllText(decisionsPath));

        // Abstract classes too: an override declared in an intermediate abstract class is inherited
        // by every concrete one and declared by none, so scanning concrete classes alone missed it.
        IEnumerable<Type> types = testAssembly.GetTypes()
            .Where(t => t.IsClass)
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
                audit.Method(type, method, isControl);
            }
        }

        return new OverrideAuditResult(audit.Violations, audit.Report());
    }

    /// <summary>
    ///     Walks up from the test binaries to the file at <paramref name="relativePath" /> in the
    ///     repository.
    /// </summary>
    public static string FindRepositoryFile(string relativePath)
        => FindRepositoryPath(relativePath, File.Exists)
            ?? throw new FileNotFoundException($"'{relativePath}' is not above '{AppContext.BaseDirectory}'.");

    private static string? FindRepositoryPath(string relativePath, Func<string, bool> exists)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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

    /// <summary>The anchor Python-Markdown gives a heading, as <c>eng/doc-links.py</c> computes it.</summary>
    private static string Slug(string heading)
    {
        string text = Regex.Replace(heading, "`([^`]*)`", "$1");
        text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, "[*_]{1,3}", string.Empty);
        text = Regex.Replace(text, @"[^\w\- ]", string.Empty).Trim().ToLowerInvariant();
        return Regex.Replace(text, @"\s+", "-");
    }

    private sealed class Audit(string defects, string decisions)
    {
        private readonly List<(string At, OverrideReasonAttribute Reason)> _audited = [];

        private readonly Dictionary<UpstreamRepository, Checkout?> _checkouts = [];

        private int _checkedReferences;

        private int _uncheckedReferences;

        public List<string> Violations { get; } = [];

        public void Method(Type type, MethodInfo method, bool isControl)
        {
            string where = $"{type.Name}.{method.Name}";
            OverrideReasonAttribute[] reasons = [.. method.GetCustomAttributes<OverrideReasonAttribute>(inherit: false)];

            if (reasons.Length == 0)
            {
                Violations.Add($"{where}: overrides a specification test and gives no reason.");
                return;
            }

            if (reasons.Length > 1
                && (reasons.Any(r => r.Case is null)
                    || reasons.Select(r => r.Case).Distinct(StringComparer.Ordinal).Count() != reasons.Length))
            {
                Violations.Add($"{where}: carries {reasons.Length} reasons, so each must name a distinct Case.");
            }

            foreach (OverrideReasonAttribute reason in reasons)
            {
                string at = reason.Case is null ? where : $"{where} [{reason.Case}]";
                Check(reason, at, method.Name, isControl);
                _audited.Add((at, reason));
            }
        }

        public string Report()
        {
            var text = new StringBuilder();
            int limits = _audited.Count(a => a.Reason is StoreLimitAttribute);
            int storeDefects = _audited.Count(a => a.Reason is StoreDefectAttribute);
            int issues = _audited.Count(a => a.Reason is StoreIssueAttribute);
            int own = _audited.Count(a => a.Reason is InfoCarrierDefectAttribute);
            int designs = _audited.Count(a => a.Reason is InfoCarrierDesignAttribute);
            int skips = _audited.Count(a => IsSkip(a.Reason));
            int deviations = _audited.Count(a => a.Reason.Deviation != DeviationKind.None);
            int silent = _audited.Count(a => a.Reason is StoreBehaviourAttribute { Justification: Upstream.GaveNoReason });

            text.AppendLine(
                $"Override audit: {_audited.Count} reasons. LIMIT {limits}, DEFECT {storeDefects}, ISSUE {issues}, "
                + $"INFOCARRIER DEFECT {own}, DESIGN {designs}. Skips {skips}. Deviations {deviations}. "
                + $"Upstream gave no reason {silent}. Upstream references checked against a checkout {_checkedReferences}, "
                + $"not checked for want of one {_uncheckedReferences}.");

            IEnumerable<string> kinds = Enum.GetValues<DeviationKind>()
                .Where(k => k != DeviationKind.None)
                .Select(k => $"{k} {_audited.Count(a => a.Reason.Deviation.HasFlag(k))}");
            text.AppendLine($"Deviations by kind: {string.Join(", ", kinds)}.");

            text.AppendLine("| Override | Label | Reference | Skip | Deviation |");
            text.AppendLine("|---|---|---|---|---|");

            foreach ((string at, OverrideReasonAttribute reason) in _audited)
            {
                string deviation = reason.Deviation == DeviationKind.None
                    ? string.Empty
                    : $"{reason.Deviation}{(reason.DeviationNote is null ? string.Empty : ": " + reason.DeviationNote)}";
                text.AppendLine($"| {at} | {Label(reason)} | {Reference(reason)} | {(IsSkip(reason) ? "yes" : "no")} | {deviation} |");
            }

            return text.ToString();
        }

        private void Check(OverrideReasonAttribute reason, string at, string methodName, bool isControl)
        {
            if (reason.Deviation.HasFlag(DeviationKind.Other) && string.IsNullOrWhiteSpace(reason.DeviationNote))
            {
                Violations.Add($"{at}: a deviation of kind Other needs a note saying what it is.");
            }

            if (reason.Deviation.HasFlag(DeviationKind.UpstreamCallsAnotherTest) && string.IsNullOrWhiteSpace(reason.DeviationNote))
            {
                Violations.Add($"{at}: says upstream calls another test and does not name it in the note.");
            }

            switch (reason)
            {
                case InfoCarrierDefectAttribute own:
                    if (isControl)
                    {
                        Violations.Add($"{at}: a wire-free control has no InfoCarrier in it, so it has no InfoCarrier defect.");
                    }

                    if (own.Issue <= 0)
                    {
                        Violations.Add($"{at}: {own.Issue} is not an issue number.");
                    }

                    break;

                case InfoCarrierDesignAttribute design:
                    if (isControl)
                    {
                        Violations.Add($"{at}: a wire-free control has no InfoCarrier in it, so it has no InfoCarrier design.");
                    }

                    if (!DecisionExists(design))
                    {
                        Violations.Add($"{at}: '{design.Decision}' is not a heading of the document it names.");
                    }

                    if (string.IsNullOrWhiteSpace(design.Justification))
                    {
                        Violations.Add($"{at}: names a decision and not which part of it applies.");
                    }

                    bool designUpstream = design.Repository != UpstreamRepository.None;
                    if (designUpstream)
                    {
                        CheckUpstream(design.Repository, design.UpstreamPath, design.UpstreamFirstLine, design.UpstreamLastLine, methodName, at);
                    }
                    else if (design.UpstreamPath is not null || design.UpstreamFirstLine != 0 || design.UpstreamLastLine != 0)
                    {
                        Violations.Add($"{at}: names an upstream file or line without a repository.");
                    }

                    if (design.Skip && !designUpstream)
                    {
                        Violations.Add($"{at}: a skip needs an upstream reference, because only upstream's own choice justifies asserting nothing.");
                    }

                    break;

                case StoreBehaviourAttribute store:
                    bool upstream = store.Repository != UpstreamRepository.None;
                    bool control = store.ControlType is not null;

                    if (isControl)
                    {
                        if (upstream || control)
                        {
                            Violations.Add($"{at}: a wire-free control is the evidence, so it names no reference.");
                        }

                        if (store.Skip)
                        {
                            Violations.Add($"{at}: a wire-free control that skips documents nothing.");
                        }
                    }
                    else
                    {
                        if (!upstream && !control)
                        {
                            Violations.Add($"{at}: says what the store does and not where that is shown.");
                        }

                        if (store.Skip && !upstream)
                        {
                            Violations.Add($"{at}: a skip needs an upstream reference, because only upstream's own choice justifies asserting nothing.");
                        }
                    }

                    if (upstream)
                    {
                        CheckUpstream(store.Repository, store.UpstreamPath, store.UpstreamFirstLine, store.UpstreamLastLine, methodName, at);

                        if (string.IsNullOrWhiteSpace(store.Justification))
                        {
                            Violations.Add($"{at}: copies no justification from upstream. Use Upstream.GaveNoReason when upstream gives none.");
                        }
                    }

                    if (control)
                    {
                        CheckControlReference(store, at);
                    }

                    if (store is StoreDefectAttribute defect && !defects.Contains($"\n### {defect.Section} ", StringComparison.Ordinal))
                    {
                        Violations.Add($"{at}: docs/upstream-defects.md has no section {defect.Section}.");
                    }

                    if (store is StoreIssueAttribute issue && (issue.Number <= 0 || !Enum.IsDefined(issue.Tracker)))
                    {
                        Violations.Add($"{at}: {issue.Key} is not an issue on a known tracker.");
                    }

                    break;
            }
        }

        /// <summary>
        ///     The reference's lines must declare the overriding test, by name, in the pinned commit's
        ///     file. Upstream's override of another test would not.
        /// </summary>
        private void CheckUpstream(UpstreamRepository repository, string? path, int firstLine, int lastLine, string methodName, string at)
        {
            if (!Enum.IsDefined(repository) || string.IsNullOrWhiteSpace(path) || firstLine < 1 || lastLine < firstLine)
            {
                Violations.Add($"{at}: the upstream reference needs a repository, a path and a line range.");
                return;
            }

            if (!_checkouts.TryGetValue(repository, out Checkout? checkout))
            {
                checkout = Checkout.Find(repository);
                _checkouts[repository] = checkout;

                if (checkout is { Head: var head } && head != Upstream.CommitOf(repository))
                {
                    Violations.Add(
                        $"{checkout.Root} is at {head ?? "an unreadable commit"}, not at {Upstream.CommitOf(repository)}, "
                        + "so no reference into it can be checked. Check it out at the pinned commit.");
                }
            }

            if (checkout is null || checkout.Head != Upstream.CommitOf(repository))
            {
                _uncheckedReferences++;
                return;
            }

            _checkedReferences++;
            string file = Path.Combine(checkout.Root, path);
            if (!File.Exists(file))
            {
                Violations.Add($"{at}: {Upstream.GitHubNameOf(repository)} has no file {path}.");
                return;
            }

            string[] lines = File.ReadAllLines(file);
            if (lastLine > lines.Length)
            {
                Violations.Add($"{at}: {path} has {lines.Length} lines, not {lastLine}.");
                return;
            }

            var declaration = new Regex($@"\b{Regex.Escape(methodName)}\s*\(");
            if (!lines[(firstLine - 1)..lastLine].Any(declaration.IsMatch))
            {
                Violations.Add($"{at}: lines {firstLine}-{lastLine} of {path} do not declare {methodName}.");
            }
        }

        /// <summary>
        ///     A self-hosted reference must name a control test that documents the SAME label for the
        ///     same case, so the wire and the control cannot disagree about what the store does.
        /// </summary>
        private void CheckControlReference(StoreBehaviourAttribute store, string at)
        {
            Type target = store.ControlType!;

            if (!target.IsDefined(typeof(WireFreeControlAttribute), inherit: false))
            {
                Violations.Add($"{at}: {target.Name} is not a wire-free control.");
                return;
            }

            MethodInfo? evidence = target.GetMethod(
                store.ControlTest!,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (evidence is null)
            {
                Violations.Add($"{at}: {target.Name} does not override {store.ControlTest}, so it documents nothing about it.");
                return;
            }

            bool agrees = evidence.GetCustomAttributes<StoreBehaviourAttribute>(inherit: false)
                .Any(e => e.GetType() == store.GetType()
                    && (store.Case is null ? e.Case is null : e.Case is null || e.Case == store.Case)
                    && SameDetail(e, store));

            if (!agrees)
            {
                string forCase = store.Case is null ? string.Empty : $" for case {store.Case}";
                Violations.Add($"{at}: {target.Name}.{store.ControlTest} does not document the same label{forCase}.");
            }
        }

        /// <summary>
        ///     An ADR is a <c>## ADR-nnn</c> heading of <c>docs/decisions.md</c>; any other decision is a
        ///     heading of the document it names, recorded where the decision was made.
        /// </summary>
        private bool DecisionExists(InfoCarrierDesignAttribute design)
        {
            if (design.Adr > 0)
            {
                return decisions.Contains($"\n## {design.Decision} ", StringComparison.Ordinal);
            }

            if (design.Document is null || design.Heading is null
                || FindRepositoryPath(design.Document, File.Exists) is not { } document)
            {
                return false;
            }

            return MarkdownHeading.Matches(MarkdownFence.Replace(File.ReadAllText(document), string.Empty))
                .Any(m => Slug(m.Groups[1].Value) == design.Heading);
        }

        private static bool SameDetail(StoreBehaviourAttribute a, StoreBehaviourAttribute b)
            => (a, b) switch
            {
                (StoreDefectAttribute x, StoreDefectAttribute y) => x.Section == y.Section,
                (StoreIssueAttribute x, StoreIssueAttribute y) => x.Tracker == y.Tracker && x.Number == y.Number,
                _ => true,
            };

        private static string Label(OverrideReasonAttribute reason)
            => reason switch
            {
                StoreLimitAttribute => "LIMIT",
                StoreDefectAttribute d => $"DEFECT {d.Section}",
                StoreIssueAttribute i => $"ISSUE {i.Key}",
                InfoCarrierDefectAttribute d => $"INFOCARRIER DEFECT #{d.Issue}",
                InfoCarrierDesignAttribute d => $"DESIGN {d.Decision}",
                _ => "?",
            };

        private static string Reference(OverrideReasonAttribute reason)
            => reason switch
            {
                InfoCarrierDefectAttribute own => $"https://github.com/azabluda/InfoCarrier.Core/issues/{own.Issue}",
                InfoCarrierDesignAttribute { Repository: not UpstreamRepository.None } d
                    => Upstream.Link(d.Repository, d.UpstreamPath!, d.UpstreamFirstLine, d.UpstreamLastLine),
                InfoCarrierDesignAttribute { Adr: > 0 } d => $"docs/decisions.md {d.Decision}",
                InfoCarrierDesignAttribute d => d.Decision,
                StoreBehaviourAttribute { Repository: not UpstreamRepository.None } s
                    => Upstream.Link(s.Repository, s.UpstreamPath!, s.UpstreamFirstLine, s.UpstreamLastLine),
                StoreBehaviourAttribute { ControlType: { } type } store => $"{type.Name}.{store.ControlTest}",
                _ => "(this control)",
            };

        private static bool IsSkip(OverrideReasonAttribute reason)
            => reason is StoreBehaviourAttribute { Skip: true } or InfoCarrierDesignAttribute { Skip: true };
    }

    /// <summary>A local checkout of an upstream repository, and the commit it is at.</summary>
    private sealed record Checkout(string Root, string? Head)
    {
        public static Checkout? Find(UpstreamRepository repository)
            => Upstream.CheckoutOf(repository) is { } relative
                && FindRepositoryPath(relative, Directory.Exists) is { } root
                    ? new Checkout(root, ReadHead(root))
                    : null;

        /// <summary>The commit <c>HEAD</c> names, read from the files so that no <c>git</c> is needed.</summary>
        private static string? ReadHead(string root)
        {
            string gitDirectory = Path.Combine(root, ".git");
            string headFile = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headFile))
            {
                return null;
            }

            string head = File.ReadAllText(headFile).Trim();
            if (!head.StartsWith("ref: ", StringComparison.Ordinal))
            {
                return head;
            }

            string reference = head["ref: ".Length..];
            string looseReference = Path.Combine(gitDirectory, reference);
            if (File.Exists(looseReference))
            {
                return File.ReadAllText(looseReference).Trim();
            }

            string packed = Path.Combine(gitDirectory, "packed-refs");
            return File.Exists(packed)
                ? File.ReadLines(packed)
                    .Select(line => line.Split(' ', 2))
                    .FirstOrDefault(parts => parts.Length == 2 && parts[1] == reference)?[0]
                : null;
        }
    }
}
