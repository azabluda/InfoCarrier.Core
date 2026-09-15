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
///         WHERE that is shown, by its reference. <c>docs/plans/v10/test-overhaul.md</c> is the
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
///         <b><see cref="Case" /> exists because one test method can hold two different store
///         behaviours.</b> A theory's tracking arm can refuse where its untracked arm crashes, and a
///         crash is never a limit. A method may therefore carry several of these, each naming a
///         distinct case.
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

    /// <summary>
    ///     Why the override's body differs from the upstream one it follows, when it does. Empty
    ///     means it is the same.
    /// </summary>
    public string? Deviation { get; set; }
}

/// <summary>
///     A reason that describes the STORE, with its evidence.
/// </summary>
/// <remarks>
///     <para>
///         <b>Three constructors, three places the evidence can be.</b> Upstream: a GitHub link to the
///         store's own test, at the commit of the release tag this repository runs, with a line
///         anchor. Self-hosted: a <c>typeof</c> and a <c>nameof</c> naming a method of a
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
    protected StoreBehaviourAttribute(string upstreamTest)
        => UpstreamTest = upstreamTest;

    /// <summary>A reason whose evidence is a test of a wire-free control in this repository.</summary>
    protected StoreBehaviourAttribute(Type controlType, string controlTest)
    {
        ControlType = controlType;
        ControlTest = controlTest;
    }

    /// <summary>The upstream test, as a link with a 40-character commit and a line anchor.</summary>
    public string? UpstreamTest { get; }

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
    public StoreLimitAttribute(string upstreamTest)
        : base(upstreamTest)
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

    /// <inheritdoc cref="StoreBehaviourAttribute(string)" />
    public StoreDefectAttribute(string section, string upstreamTest)
        : base(upstreamTest)
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
    public StoreIssueAttribute(string key)
        => Key = key;

    /// <inheritdoc cref="StoreBehaviourAttribute(string)" />
    public StoreIssueAttribute(string key, string upstreamTest)
        : base(upstreamTest)
        => Key = key;

    /// <inheritdoc cref="StoreBehaviourAttribute(Type, string)" />
    public StoreIssueAttribute(string key, Type controlType, string controlTest)
        : base(controlType, controlTest)
        => Key = key;

    /// <summary>The tracker key, such as <c>"EF-250"</c> or <c>"dotnet/efcore#36400"</c>.</summary>
    public string Key { get; }
}

/// <summary>
///     A defect in THIS provider that could not be fixed yet, with the issue of this repository
///     that tracks it. A fix comes first; this is the last resort, and only the owner files the issue.
/// </summary>
public sealed class InfoCarrierDefectAttribute(string issue) : OverrideReasonAttribute
{
    /// <summary>The issue, as <c>https://github.com/azabluda/InfoCarrier.Core/issues/&lt;n&gt;</c>.</summary>
    public string Issue { get; } = issue;
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
///         <b>The reference is the decision</b>, as <c>ADR-nnn</c>, and <see cref="OverrideAudit" />
///         checks that <c>docs/decisions.md</c> has that heading. <see cref="Justification" /> says
///         which part of the decision applies. A skip still needs <see cref="UpstreamTest" />, for the
///         same reason as a store label: only upstream's own choice justifies asserting nothing.
///     </para>
/// </remarks>
public sealed class InfoCarrierDesignAttribute(string decision) : OverrideReasonAttribute
{
    /// <summary>The recorded decision, such as <c>"ADR-010"</c>.</summary>
    public string Decision { get; } = decision;

    /// <summary>Which part of the decision makes this test behave differently. Required.</summary>
    public string? Justification { get; set; }

    /// <summary>An upstream test that makes the same choice, required only with <see cref="Skip" />.</summary>
    public string? UpstreamTest { get; set; }

    /// <summary>The override asserts nothing. Requires <see cref="UpstreamTest" />.</summary>
    public bool Skip { get; set; }
}

/// <summary>Values for <see cref="OverrideReasonAttribute.Deviation" /> that recur across many overrides.</summary>
public static class Deviations
{
    /// <summary>The upstream override also asserts SQL, which this client never emits.</summary>
    public const string SqlNotAsserted =
        "EF's own override also asserts the SQL the store received. This client emits no SQL, because the "
        + "server's provider writes it, so the SQL assertion is omitted and the rest is the same.";

    /// <summary>The upstream override asserts the store's own exception, which cannot cross the wire as itself.</summary>
    public const string StoreExceptionCrossesAsData =
        "EF's own override asserts the store's exception type. Over the wire that exception arrives as "
        + "InfoCarrierServerException, carrying the store exception's type name and message, and those are "
        + "asserted instead.";
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

/// <summary>Values for <see cref="StoreBehaviourAttribute.Justification" /> and the upstream links.</summary>
public static class Upstream
{
    /// <summary>
    ///     EF Core's repository at the <c>v10.0.1</c> tag, which <c>subrepos/efcore</c> is checked out
    ///     at and the specification packages are built from. A link is this plus a path and a line.
    /// </summary>
    public const string EfCore = "https://github.com/dotnet/efcore/blob/a6217e3438ca1fb430079f2626056c1a11581927/";

    /// <summary>Upstream overrides the test and says nothing about why, which the audit counts.</summary>
    public const string GaveNoReason = "(upstream gives no reason)";
}
