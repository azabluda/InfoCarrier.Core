// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Stands in, on the client, for the receiver of a user-defined function mapped as an instance
///     method on the context. Serialized as <c>ServerContextStubNode</c>; the server puts its own
///     context in its place.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a node of our own rather than the constant EF produced.</b> A call such as
///         <c>context.CustomerOrderCount(c.Id)</c> funcletizes to a receiver holding the live
///         client <see cref="Microsoft.EntityFrameworkCore.DbContext" />, and
///         <c>ServerBoundaryAnalyzer.CarriesTheClientsContext</c> refuses that — rightly, because a
///         context is an object graph with a change tracker and a service provider, and the server
///         has one of its own. The refusal was measured as the right answer: admitted, the server
///         tried to rebuild a <c>DbContext</c> from the payload; not admitted, the client fetched
///         the whole table and ran the function locally.
///     </para>
///     <para>
///         <b>What that refusal cost, and why this exists.</b> In a PREDICATE the refusal is
///         correct and stays: the caller gets EF's own translation failure. In a PROJECTION nothing
///         refuses it, because client evaluation in a final projection is legal, so the client RAN
///         the function — and EF's own specification contexts give those methods a body that throws
///         precisely to prove they were translated rather than run. That is what
///         <c>UdfDbFunctionTestBase</c> reports as <see cref="System.NotImplementedException" />.
///     </para>
///     <para>
///         <b>This is narrow on purpose.</b> Only a receiver whose method the model maps with
///         <c>HasDbFunction</c> is rewritten, and only before the boundary is drawn. A context
///         reaching the boundary any other way is still refused, and the pin that asserts so is
///         still there.
///     </para>
/// </remarks>
/// <param name="type">The context type the call's method is declared on.</param>
/// <param name="clientContext">
///     The context this stood in for, which <see cref="Reduce" /> gives back.
/// </param>
public sealed class ServerContextExpression(Type type, object clientContext) : Expression
{
    /// <inheritdoc />
    public override ExpressionType NodeType
        => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type { get; } = type;

    /// <inheritdoc />
    /// <remarks>
    ///     <b>Reducible, and that is not a detail.</b> This node is put in before the boundary is
    ///     drawn, and the boundary may leave the call on the CLIENT — EF's own
    ///     <c>Scalar_Function_ClientEval_...</c> tests require exactly that. What the client
    ///     compiles must therefore still be a runnable tree, and reducing to the constant this
    ///     replaced makes the kept case behave precisely as it did before the rewrite existed.
    ///     Without it the client compiler answers <c>ArgumentException: must be reducible node</c>.
    /// </remarks>
    public override bool CanReduce
        => true;

    /// <inheritdoc />
    public override Expression Reduce()
        => Constant(clientContext, Type);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => this;
}
