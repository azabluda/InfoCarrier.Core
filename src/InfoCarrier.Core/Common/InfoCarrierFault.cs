// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     A server-side failure, in a form a wire can carry (wire-protocol W5).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this has to exist at all.</b> In-process, a server exception reaches the caller
///         by simply propagating — it is the same object on the same stack. No network transport
///         can do that. So a suite that only ever runs in-process is not testing the error
///         behaviour it appears to test; it is testing the absence of a wire. That is the same
///         illusion the type allowlist was introduced to break, and roadmap M2's re-scoping note
///         is the precedent.
///     </para>
///     <para>
///         <b>What fidelity means here</b> is set by what callers actually depend on: the
///         exception's <em>type</em>, its <em>message</em>, and its <em>inner chain</em>. EF's
///         spec tests assert all three — <c>Assert.Throws&lt;InvalidOperationException&gt;</c>
///         against an exact <c>CoreStrings</c> message — so a fault that lost any of them would be
///         detected by thousands of tests, which is the point of routing them through it.
///     </para>
///     <para>
///         <b>The stack trace is carried but not re-thrown.</b> A rehydrated exception gets a
///         stack from where the client threw it; the server's is preserved in
///         <see cref="System.Exception.Data" /> under
///         <c>"InfoCarrier.ServerStackTrace"</c> instead. Splicing it into the exception's own
///         stack would mean lying about where the client is, and the alternative — appending it to
///         the message — would break every message assertion.
///     </para>
/// </remarks>
public sealed record InfoCarrierFault
{
    /// <summary>
    ///     The exception's CLR type name, without assembly or version — the same assembly-free
    ///     identity <c>TypeNode</c> uses, for the same reason.
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    ///     The exception message, verbatim.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///     The server-side stack trace, for diagnosis. Never parsed, never re-thrown.
    /// </summary>
    public string? StackTrace { get; init; }

    /// <summary>
    ///     The inner failure, if the server's exception had one. Rebuilt recursively, because a
    ///     caller that inspects <see cref="System.Exception.InnerException" /> is inspecting a
    ///     chain rather than one link.
    /// </summary>
    public InfoCarrierFault? Inner { get; init; }

    /// <summary>
    ///     For a <c>DbUpdateException</c>: the correlation ids — positions in the submitted
    ///     <c>SaveChangesRequest.Entries</c> list — of the entries the store actually rejected
    ///     (#70). <see langword="null" /> when the server could not narrow it, or the failure is
    ///     not an update failure.
    /// </summary>
    /// <remarks>
    ///     Every other EF provider's <c>DbUpdateException.Entries</c> names the one entry the
    ///     store rejected; without this the client can only re-raise naming <em>every</em> entry
    ///     it sent, because the server's own update entries belong to a context that is disposed
    ///     with the request. Matching by key value does not help when the rejected row is
    ///     <c>Added</c> with a store-generated key — the server has no key for it either — so what
    ///     crosses is the ordinal, which the server knows from the order it replayed the batch.
    ///     The server stashes it on <see cref="System.Exception.Data" /> under
    ///     <see cref="InfoCarrierFaultMapper.FailedCorrelationIdsKey" />; this field is how it
    ///     survives the wire, and <c>Rehydrate</c> puts it back on <c>Data</c>.
    /// </remarks>
    public int[]? FailedCorrelationIds { get; init; }
}
