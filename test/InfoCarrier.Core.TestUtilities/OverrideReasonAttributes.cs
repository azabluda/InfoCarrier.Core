// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Why an override of a specification test changes what that test expects.
/// </summary>
/// <remarks>
///     <para>
///         <b>EVERY OVERRIDE OF A SPECIFICATION TEST CARRIES ONE OF THESE, AND
///         <see cref="OverrideAudit" /> FAILS THE BUILD ON ONE THAT DOES NOT.</b> The suite is green
///         and there is no ratchet, which is how EF Core's own providers work. This repository is
///         stricter in one way only: an override has to say WHAT the store does, by its label, and
///         WHERE that is shown, by its reference. <c>docs/test-policy.md</c> is the
///         reading.
///     </para>
///     <para>
///         <b>The label is the attribute type; the reference is its arguments.</b>
///         <see cref="StoreLimitAttribute" />, <see cref="StoreDefectAttribute" /> and
///         <see cref="StoreIssueAttribute" /> describe the store.
///         <see cref="InfoCarrierDefectAttribute" /> describes this provider, and is the last
///         resort after a fix. <see cref="InfoCarrierDesignAttribute" /> describes this provider too,
///         where it differs on purpose.
///     </para>
///     <para>
///         <b>Every value that recurs is typed, not text</b> (2026-09-15, the owner's request): an
///         upstream reference is a repository, a path and a line range, an issue is a tracker and a
///         number, a decision is an ADR number, and a deviation is a set of
///         <see cref="DeviationKind" /> flags. Text is left only where it is somebody's words:
///         upstream's justification, and a note on a deviation. A typed value can be counted, and an
///         upstream reference can be checked against the checkout it names.
///     </para>
///     <para>
///         <b><see cref="Case" /> exists because one test method can hold two different store
///         behaviours.</b> A theory's tracking arm can refuse where its untracked arm crashes, and a
///         crash is never a limit. A method may therefore carry several store reasons, each naming a
///         distinct case.
///     </para>
///     <para>
///         <b>And a method can carry a reason from each side for the same case</b> (the owner,
///         2026-09-15). A store reason says what EF's own provider test does and where; an InfoCarrier
///         reason says why this test differs from that one. SQLite refuses a query with
///         <c>ApplyNotSupported</c>, so EF's SQLite test expects that, and this provider refuses the
///         same query earlier by ADR-010: <c>[StoreLimit]</c> and <c>[InfoCarrierDesign(10)]</c>
///         together. One reason alone would lose either the link to EF's test or the decision.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public abstract class OverrideReasonAttribute : Attribute
{
    /// <summary>
    ///     The theory case this reason covers, such as <c>"TrackAll"</c>; <see langword="null" /> for
    ///     every case of the method.
    /// </summary>
    public string? Case { get; set; }

    /// <summary>How the override's body differs from the upstream one it follows. None means it is the same.</summary>
    public DeviationKind Deviation { get; set; }

    /// <summary>Detail a deviation kind cannot carry. Required with <see cref="DeviationKind.Other" />.</summary>
    public string? DeviationNote { get; set; }
}

/// <summary>The repositories an upstream reference can name, each pinned to the commit this repository runs.</summary>
public enum UpstreamRepository
{
    /// <summary>No upstream reference.</summary>
    None = 0,

    /// <summary><c>dotnet/efcore</c> at <c>v10.0.1</c>, the version of the specification packages.</summary>
    EfCore,

    /// <summary><c>mongodb/mongo-efcore-provider</c> at <c>v10.0.3</c>, the provider Tier D runs.</summary>
    MongoEfCore,
}

/// <summary>The trackers an issue can be on.</summary>
public enum IssueTracker
{
    /// <summary>EF Core's GitHub issues, rendered <c>dotnet/efcore#n</c>.</summary>
    EfCore = 1,

    /// <summary>The MongoDB EF provider's Jira project, rendered <c>EF-n</c>.</summary>
    MongoEfCore,
}

/// <summary>How an override's body differs from the upstream override it follows.</summary>
[Flags]
public enum DeviationKind
{
    /// <summary>The body is upstream's.</summary>
    None = 0,

    /// <summary>
    ///     Upstream also asserts the SQL the store received. This client emits none, so that part is
    ///     omitted. #111 tracked asserting it and was closed on 2026-09-25 in favour of #167, which
    ///     compares every test's statements with plain EF Core instead.
    /// </summary>
    SqlNotAsserted = 1 << 0,

    /// <summary>
    ///     Upstream asserts the store's exception type. Over the wire it arrives as
    ///     <c>InfoCarrierServerException</c> carrying that type's name and message, which are asserted.
    /// </summary>
    /// <remarks>
    ///     <b>Mechanical, and not an InfoCarrier difference</b> (the owner, 2026-09-15). The store
    ///     refuses at the same place with the same message; only the exception's type differs, and
    ///     one decision covers every server error: <c>InfoCarrierFaultMapper</c> rebuilds a type only
    ///     through a <c>(string)</c> or <c>(string, Exception)</c> constructor, and
    ///     <c>docs/security-review.md</c> §7 records why. Labelling it per test would count only the
    ///     tests whose upstream assertion names the exact type.
    /// </remarks>
    StoreExceptionAsData = 1 << 1,

    /// <summary>Upstream's override calls a different base test. The note names it.</summary>
    UpstreamCallsAnotherTest = 1 << 2,

    /// <summary>Upstream skips the test, or asserts nothing. This override asserts.</summary>
    UpstreamAssertsNothing = 1 << 3,

    /// <summary>
    ///     Upstream expects a refusal and this provider answers, so the override asserts the answer.
    ///     This provider's behaviour, so legal only on an InfoCarrier reason.
    /// </summary>
    AnswerNotRefusal = 1 << 4,

    /// <summary>
    ///     This provider refuses earlier than the store does, with a different refusal. This provider's
    ///     behaviour, so legal only on an InfoCarrier reason.
    /// </summary>
    RefusedEarlier = 1 << 5,

    /// <summary>Upstream has no override for this base, so the reference is its override of the same test in another family.</summary>
    BorrowedFromAnotherBase = 1 << 6,

    /// <summary>The base's assertion cannot state the behaviour, so the override writes the base's query out.</summary>
    QueryWrittenOut = 1 << 7,

    /// <summary>
    ///     The answer is upstream's, and the server's SQL differs from what plain EF Core runs for the
    ///     same test: the case's entries in its class's <c>.wire.sql</c> and <c>.direct.sql</c> differ
    ///     (#167, <c>docs/sql-capture.md</c> §9). This provider's behaviour, so legal only on an
    ///     InfoCarrier reason.
    /// </summary>
    /// <remarks>
    ///     <c>SqlCaptureComplianceTest</c> fails when no case the reason covers differs, so a fix that
    ///     removes the difference removes the reason too, and a pass-through override with it.
    /// </remarks>
    SqlDiffers = 1 << 8,

    /// <summary>Anything else, described in <see cref="OverrideReasonAttribute.DeviationNote" />.</summary>
    Other = 1 << 30,
}

/// <summary>
///     A reason that describes the STORE, with its evidence.
/// </summary>
/// <remarks>
///     <para>
///         <b>Three constructors, three places the evidence can be.</b> Upstream: the store's own test,
///         as a repository, a path and a line range, at the commit that repository is pinned to.
///         Self-hosted: a <c>typeof</c> and a <c>nameof</c> naming a method of a
///         <see cref="WireFreeControlAttribute" /> class, which the compiler keeps honest. None:
///         permitted only INSIDE such a class, because the control is the evidence.
///     </para>
///     <para>
///         <b>A skip is permitted only with an upstream reference.</b> It asserts nothing, so the only
///         thing that justifies it is that upstream made the same choice for the same store. Where
///         upstream has no test, there is nothing to copy and the override states the behaviour.
///     </para>
/// </remarks>
public abstract class StoreBehaviourAttribute : OverrideReasonAttribute
{
    /// <summary>A reason inside a wire-free control, which is its own evidence.</summary>
    protected StoreBehaviourAttribute()
    {
    }

    /// <summary>A reason whose evidence is the store's own test upstream.</summary>
    protected StoreBehaviourAttribute(UpstreamRepository repository, string path, int firstLine, int lastLine)
    {
        Repository = repository;
        UpstreamPath = path;
        UpstreamFirstLine = firstLine;
        UpstreamLastLine = lastLine;
    }

    /// <summary>A reason whose evidence is a test of a wire-free control in this repository.</summary>
    protected StoreBehaviourAttribute(Type controlType, string controlTest)
    {
        ControlType = controlType;
        ControlTest = controlTest;
    }

    /// <summary>The repository of the upstream test, or <see cref="UpstreamRepository.None" />.</summary>
    public UpstreamRepository Repository { get; }

    /// <summary>The upstream test's file, relative to its repository's root.</summary>
    public string? UpstreamPath { get; }

    /// <summary>The first line of the upstream override.</summary>
    public int UpstreamFirstLine { get; }

    /// <summary>The last line of the upstream override.</summary>
    public int UpstreamLastLine { get; }

    /// <summary>The wire-free control class whose test shows the behaviour.</summary>
    public Type? ControlType { get; }

    /// <summary>The name of that control test.</summary>
    public string? ControlTest { get; }

    /// <summary>
    ///     Upstream's own justification, copied verbatim, or <see cref="Upstream.GaveNoReason" />.
    ///     Required with an upstream reference.
    /// </summary>
    public string? Justification { get; set; }

    /// <summary>The override asserts nothing. Requires an upstream reference.</summary>
    public bool Skip { get; set; }
}

/// <summary><c>LIMIT</c>: the store refuses by design.</summary>
public sealed class StoreLimitAttribute : StoreBehaviourAttribute
{
    /// <inheritdoc />
    public StoreLimitAttribute()
    {
    }

    /// <inheritdoc />
    public StoreLimitAttribute(UpstreamRepository repository, string path, int firstLine, int lastLine)
        : base(repository, path, firstLine, lastLine)
    {
    }

    /// <inheritdoc />
    public StoreLimitAttribute(Type controlType, string controlTest)
        : base(controlType, controlTest)
    {
    }
}

/// <summary>
///     <c>DEFECT</c>: the store crashes, or answers wrongly. Never a limit, because a store that means
///     "no" says so.
/// </summary>
public sealed class StoreDefectAttribute : StoreBehaviourAttribute
{
    /// <inheritdoc cref="StoreBehaviourAttribute()" />
    public StoreDefectAttribute(string section)
        => Section = section;

    /// <inheritdoc cref="StoreBehaviourAttribute(UpstreamRepository, string, int, int)" />
    public StoreDefectAttribute(string section, UpstreamRepository repository, string path, int firstLine, int lastLine)
        : base(repository, path, firstLine, lastLine)
        => Section = section;

    /// <inheritdoc cref="StoreBehaviourAttribute(Type, string)" />
    public StoreDefectAttribute(string section, Type controlType, string controlTest)
        : base(controlType, controlTest)
        => Section = section;

    /// <summary>The section of <c>docs/upstream-defects.md</c> that records it, such as <c>"1.6"</c>.</summary>
    public string Section { get; }
}

/// <summary>
///     <c>ISSUE</c>: an entry on a tracker already covers the behaviour. Any tracker, not only the
///     store's: EF Core's own issues qualify.
/// </summary>
public sealed class StoreIssueAttribute : StoreBehaviourAttribute
{
    /// <inheritdoc cref="StoreBehaviourAttribute()" />
    public StoreIssueAttribute(IssueTracker tracker, int number)
        => (Tracker, Number) = (tracker, number);

    /// <inheritdoc cref="StoreBehaviourAttribute(UpstreamRepository, string, int, int)" />
    public StoreIssueAttribute(IssueTracker tracker, int number, UpstreamRepository repository, string path, int firstLine, int lastLine)
        : base(repository, path, firstLine, lastLine)
        => (Tracker, Number) = (tracker, number);

    /// <inheritdoc cref="StoreBehaviourAttribute(Type, string)" />
    public StoreIssueAttribute(IssueTracker tracker, int number, Type controlType, string controlTest)
        : base(controlType, controlTest)
        => (Tracker, Number) = (tracker, number);

    /// <summary>The tracker the issue is on.</summary>
    public IssueTracker Tracker { get; }

    /// <summary>The issue's number on that tracker.</summary>
    public int Number { get; }

    /// <summary>The issue as its tracker writes it, such as <c>dotnet/efcore#36400</c> or <c>EF-250</c>.</summary>
    public string Key
        => Tracker switch
        {
            IssueTracker.EfCore => $"dotnet/efcore#{Number}",
            IssueTracker.MongoEfCore => $"EF-{Number}",
            _ => $"?{Number}",
        };
}

/// <summary>
///     A defect in THIS provider that could not be fixed yet, with the issue of this repository
///     that tracks it. A fix comes first; this is the last resort, and only the owner files the issue.
/// </summary>
public sealed class InfoCarrierDefectAttribute(int issue) : OverrideReasonAttribute
{
    /// <summary>The number of the issue in <c>azabluda/InfoCarrier.Core</c>.</summary>
    public int Issue { get; } = issue;
}

/// <summary>
///     <c>DESIGN</c>: this provider behaves differently from the store ON PURPOSE, as a recorded
///     decision says. Neither a store behaviour nor a defect.
/// </summary>
/// <remarks>
///     <para>
///         <b>Added when the relational tiers were converted (2026-09-15), for the overrides the four
///         other labels could not describe.</b> A client that refuses to evaluate a filter it cannot
///         send, or that answers a projection the store's own provider refuses, fails EF's SQLite
///         test on the same store. The store is not the reason, and nothing is broken.
///     </para>
///     <para>
///         <b>The reference is the decision</b>: an ADR number, or a document and one of its headings
///         for a decision recorded where it was made (the raw-SQL grant is in
///         <c>docs/security-review.md</c>). <see cref="OverrideAudit" /> checks that the heading
///         exists. <see cref="Justification" /> says which part of the decision applies. A skip still
///         needs an upstream reference, for the same reason as a store label: only upstream's own
///         choice justifies asserting nothing.
///     </para>
/// </remarks>
public sealed class InfoCarrierDesignAttribute : OverrideReasonAttribute
{
    /// <summary>A decision of <c>docs/decisions.md</c>.</summary>
    /// <param name="adr">The ADR's number: 10 for ADR-010.</param>
    public InfoCarrierDesignAttribute(int adr)
        => Adr = adr;

    /// <summary>A decision recorded in another document.</summary>
    /// <param name="document">The document, relative to the repository root.</param>
    /// <param name="heading">The anchor of the heading that records the decision.</param>
    public InfoCarrierDesignAttribute(string document, string heading)
        => (Document, Heading) = (document, heading);

    /// <summary>The ADR number, or 0 when the decision is in <see cref="Document" />.</summary>
    public int Adr { get; }

    /// <summary>The document that records the decision, when it is not an ADR.</summary>
    public string? Document { get; }

    /// <summary>The anchor of the heading in <see cref="Document" />.</summary>
    public string? Heading { get; }

    /// <summary>The decision as the audit reports it: <c>ADR-010</c>, or <c>document#heading</c>.</summary>
    public string Decision
        => Adr > 0 ? $"ADR-{Adr:D3}" : $"{Document}#{Heading}";

    /// <summary>Which part of the decision makes this test behave differently. Required.</summary>
    public string? Justification { get; set; }

    /// <summary>The repository of an upstream test that makes the same choice. Required only with <see cref="Skip" />.</summary>
    public UpstreamRepository Repository { get; set; }

    /// <summary>That upstream test's file, relative to its repository's root.</summary>
    public string? UpstreamPath { get; set; }

    /// <summary>The first line of that upstream override.</summary>
    public int UpstreamFirstLine { get; set; }

    /// <summary>The last line of that upstream override.</summary>
    public int UpstreamLastLine { get; set; }

    /// <summary>The override asserts nothing. Requires an upstream reference.</summary>
    public bool Skip { get; set; }
}

/// <summary>Decisions recorded outside the ADR log, for <see cref="InfoCarrierDesignAttribute(string, string)" />.</summary>
public static class Decisions
{
    /// <summary><c>docs/security-review.md</c>, where the raw-SQL grant is decided.</summary>
    public const string SecurityReview = "docs/security-review.md";

    /// <summary>
    ///     Raw SQL crosses only when the server grants it with <c>AddInfoCarrierArbitrarySqlExecution</c>
    ///     (#60, R95).
    /// </summary>
    public const string RawSqlGrant =
        "5a-amendment-raw-sql-60-r95-and-why-it-is-a-change-of-posture-rather-than-a-wider-list";

    /// <summary><c>docs/architecture.md</c>, where the client's service and convention set is decided.</summary>
    public const string Architecture = "docs/architecture.md";

    /// <summary>
    ///     D7: which of EF's relational services and conventions the client runs. Everything past the
    ///     capture point is the server's, and a convention or validator that decides store layout is
    ///     not run on the client.
    /// </summary>
    public const string ClientServices =
        "d7-the-client-gets-efs-core-services-and-nobody-had-listed-what-the-relational-set-adds";

    /// <summary><c>docs/plans/v10/findings.md</c>, where findings and the scope decisions they led to are recorded.</summary>
    public const string Findings = "docs/plans/v10/findings.md";

    /// <summary>
    ///     R138: the boundary analyzer does not ask the client model whether a member is mapped. The
    ///     fix is written and priced, and not shipped, because the owner removed split models from the
    ///     scope on 2026-09-04.
    /// </summary>
    public const string UnmappedMembers =
        "the-boundary-analyzer-does-not-consult-the-client-model-for-member-mappability-r138-2026-09-03";

    /// <summary><c>website/docs/limitations.md</c>, which tells a consumer what is not supported.</summary>
    public const string Limitations = "website/docs/limitations.md";

    /// <summary>The one unsupported scenario, a property-bag complex collection holding a primitive collection.</summary>
    public const string NotSupported = "not-supported";
}

/// <summary>
///     Marks a class that runs specification bases with InfoCarrier REMOVED, so that its overrides
///     are the store's behaviour measured directly, and are evidence rather than claims.
/// </summary>
/// <remarks>
///     Its overrides carry a label and no reference, may not skip, and may not carry
///     <see cref="InfoCarrierDefectAttribute" />: there is no InfoCarrier in it.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WireFreeControlAttribute : Attribute;

/// <summary>Values for <see cref="StoreBehaviourAttribute.Justification" />, and where each repository is pinned.</summary>
public static class Upstream
{
    /// <summary>Upstream overrides the test and says nothing about why, which the audit counts.</summary>
    public const string GaveNoReason = "(upstream gives no reason)";

    /// <summary>The commit a repository is pinned to: the release tag of the version this repository runs.</summary>
    public static string CommitOf(UpstreamRepository repository)
        => repository switch
        {
            UpstreamRepository.EfCore => "a6217e3438ca1fb430079f2626056c1a11581927",
            UpstreamRepository.MongoEfCore => "de93261da6d989bd12db8815a3a672dcf28bafb1",
            _ => throw new ArgumentOutOfRangeException(nameof(repository), repository, null),
        };

    /// <summary>The repository's GitHub name.</summary>
    public static string GitHubNameOf(UpstreamRepository repository)
        => repository switch
        {
            UpstreamRepository.EfCore => "dotnet/efcore",
            UpstreamRepository.MongoEfCore => "mongodb/mongo-efcore-provider",
            _ => throw new ArgumentOutOfRangeException(nameof(repository), repository, null),
        };

    /// <summary>
    ///     Where a checkout of the repository sits in this repository, if one does. <c>subrepos/</c> is
    ///     git-ignored, so a CI runner has none and the audit cannot check a reference's lines there.
    /// </summary>
    public static string? CheckoutOf(UpstreamRepository repository)
        => repository switch
        {
            UpstreamRepository.EfCore => "subrepos/efcore",
            _ => null,
        };

    /// <summary>The permanent link to a line range at the pinned commit.</summary>
    public static string Link(UpstreamRepository repository, string path, int firstLine, int lastLine)
        => $"https://github.com/{GitHubNameOf(repository)}/blob/{CommitOf(repository)}/{path}#L{firstLine}"
            + (lastLine > firstLine ? $"-L{lastLine}" : string.Empty);
}
