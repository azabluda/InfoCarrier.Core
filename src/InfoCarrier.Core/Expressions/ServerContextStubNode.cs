// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Stands in for the receiver of a user-defined function mapped as an instance method on the
///     context. Rebound on the server to the server's own <see cref="Microsoft.EntityFrameworkCore.DbContext" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>It carries a type and nothing else, and that is the whole point.</b> A client context
///         is an object graph with a change tracker, a service provider and a connection; none of
///         it crosses, and this provider refuses a context constant everywhere else for exactly
///         that reason. What crosses here is the <em>role</em>: "the context this query runs
///         against". The server fills the role with its own.
///     </para>
///     <para>
///         <b><see cref="Type" /> is the client's context type, and the server checks it rather
///         than trusting it.</b> The method being called is declared on that type, so the server's
///         context must be assignable to it or the rebuilt call cannot bind. Where it is not, the
///         server says so by name instead of failing inside reflection.
///     </para>
/// </remarks>
public sealed record ServerContextStubNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind
        => NodeKind.ServerContextStub;

    /// <summary>
    ///     The context type the call's method is declared on.
    /// </summary>
    public required TypeNode Type { get; init; }
}
