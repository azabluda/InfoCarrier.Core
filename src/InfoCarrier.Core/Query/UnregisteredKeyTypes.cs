// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Names the key types that keep an operator on this client because the server was never told
///     about them, so the <see cref="InfoCarrierEventId.QuerySplit" /> event can say what to pass to
///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>The case this exists for is a key of a type the application declared.</b> A
///         <c>GroupBy</c>, <c>Join</c>, <c>GroupJoin</c> or <c>DistinctBy</c> keyed on one stays here
///         when the allowlist refuses the type, and the server then sends every row. There is no
///         error, on purpose: <c>QuerySplitter.RejectClientEvaluation</c> lets a client TYPE through
///         because refusing it once cost 235 passing tests. Registering the type is the caller's
///         one-line fix, and <c>ServerParameterizationTest</c> measures both halves. Until this
///         class, nothing named the type, so the caller had to find it by reading the query.
///     </para>
///     <para>
///         <b>Only a type a row-deciding argument CONSTRUCTS.</b> A projection is not a key: what it
///         constructs is reassembled here after the rows are chosen, and naming it would advise a
///         registration that saves nothing. A called method's declaring type is not named either,
///         although the boundary refuses it too, because registering it would ask the server to run
///         the caller's code. That is not what <c>AllowTypes</c> is for.
///     </para>
///     <para>
///         <b>A compiler-generated type is not named</b>, because a caller cannot write it in
///         <c>typeof</c>. An anonymous key ships already, and <c>ServerParameterizationTest</c>
///         records that measurement.
///     </para>
///     <para>
///         <b>Walked only when something is listening</b>, as <see cref="RowRemovingOperators" />
///         is, and for the same reason: a split is decided per execution.
///     </para>
/// </remarks>
internal static class UnregisteredKeyTypes
{
    /// <summary>
    ///     The types the residual's row-deciding arguments construct that the server was not told
    ///     about, each once and in the order they are met.
    /// </summary>
    /// <param name="residual">The part of the query this client runs.</param>
    /// <param name="allowlist">The types the server accepts.</param>
    public static IReadOnlyList<Type> Find(Expression residual, TypeAllowlist allowlist)
    {
        var found = new List<Type>();
        new OperatorWalker(allowlist, found).Visit(residual);
        return found;
    }

    /// <summary>
    ///     The sentence the split event ends with, for a residual that <see cref="Find" /> names
    ///     types in.
    /// </summary>
    /// <param name="types">What <see cref="Find" /> returned; not empty.</param>
    public static string Describe(IReadOnlyList<Type> types)
        => "These key types are not registered with AllowTypes, so their operators stay on this client: "
            + string.Join(", ", types.Select(t => t.ShortDisplayName()))
            + ". Registering them on the client and on the server can send those operators to the server.";

    private sealed class OperatorWalker(TypeAllowlist allowlist, List<Type> found) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable))
            {
                var constructed = new ConstructedTypeCollector(allowlist, found);
                foreach (Expression argument in QuerySplitter.RowDecidingArguments(node))
                {
                    constructed.Visit(argument);
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    private sealed class ConstructedTypeCollector(TypeAllowlist allowlist, List<Type> found) : ExpressionVisitor
    {
        // `VisitMemberInit` reaches its constructor through here, so an initializer needs no case
        // of its own.
        protected override Expression VisitNew(NewExpression node)
        {
            ArgumentNullException.ThrowIfNull(node);

            Type type = node.Type;
            if (!type.IsGenericParameter
                && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && !allowlist.IsAllowed(type)
                && !found.Contains(type))
            {
                found.Add(type);
            }

            return base.VisitNew(node);
        }
    }
}
