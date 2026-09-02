// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     CLR types a <em>server</em> permits a wire payload to name, beyond the ones its model
///     implies (ADR-008 constraint 2).
/// </summary>
/// <remarks>
///     <para>
///         The server half of the seam whose client half is
///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" />, registered with
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierAllowedTypes" />.
///         <b>The two halves must agree</b>, exactly as ADR-012 requires of value mappers: a type
///         admitted on the client only produces a query the server refuses to read, and one
///         admitted on the server only produces a query the client refuses to send.
///     </para>
///     <para>
///         <b>Only this half is a security boundary.</b> The client list decides what may be
///         <em>sent</em> by code the application already controls. This one decides what a
///         <em>payload</em> may name, which is the threat model <c>docs/security-review.md</c>
///         states. Read its section 2 before registering anything, because the safety of that
///         stage is a conjunction across several clauses rather than one check.
///     </para>
///     <para>
///         Resolved as an <c>IEnumerable</c>, so several registrations compose and none replaces
///         another.
///     </para>
/// </remarks>
public interface IInfoCarrierAllowedTypes
{
    /// <summary>
    ///     The types to admit.
    /// </summary>
    IEnumerable<Type> Types { get; }
}
