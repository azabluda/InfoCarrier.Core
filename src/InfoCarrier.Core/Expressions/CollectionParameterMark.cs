// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     The mark that keeps a compiled query's collection parameter a parameter on the server. The
///     client writes it and the server reads it, and both halves are here so they cannot drift.
/// </summary>
/// <remarks>
///     <para>
///         <b>The client writes it</b> where a compiled query applies an operator to a collection
///         parameter, as in <c>ids.Skip(1).Contains(x.Id)</c>, and since 2026-09-22 also where the
///         operator returns a scalar, as <c>ids.Count()</c> does; until then the text read "where a
///         compiled query transforms a collection parameter". The parameter crosses the wire as a
///         value in a <see cref="ParameterBox{T}" />, and without the mark the server's funcletizer
///         evaluates the operator over it, so the statement changes with the number of values (#122).
///         The mark is EF's own <c>EF.MultipleParameters</c>, the instruction EF has for "do not
///         evaluate this collection".
///     </para>
///     <para>
///         <b>The server reads it</b>, because that instruction also names a collection mode, EF's
///         default, and the client cannot know the mode the server is configured with. EF prefers a
///         mode on one parameter to the option, so a server set to <c>Constant</c> ran parameters
///         where EF runs literals, and one set to <c>Parameter</c> ran one parameter per value where
///         EF runs a single one (measured 2026-09-21). The server replaces the mark with EF's marker
///         for its own mode. EF translates a collection parameter in the mode the parameter carries
///         or, when it carries none, in the option's, so the two give the same statement.
///     </para>
///     <para>
///         <b>A marker the caller wrote is not the client's mark, and must keep the caller's
///         mode.</b> They are told apart by what they wrap: the client marks the <c>Value</c> of a
///         <see cref="ParameterBox{T}" />, and the client substitutes the argument of every EF marker
///         the caller wrote as a plain constant.
///     </para>
/// </remarks>
internal static class CollectionParameterMark
{
    // Taken from delegates so the trimmer sees them (R149).
    private static readonly MethodInfo MultipleParameters =
        ((Func<int[], int[]>)EF.MultipleParameters).Method.GetGenericMethodDefinition();

    private static readonly MethodInfo Constant =
        ((Func<int[], int[]>)EF.Constant).Method.GetGenericMethodDefinition();

    private static readonly MethodInfo Parameter =
        ((Func<int[], int[]>)EF.Parameter).Method.GetGenericMethodDefinition();

    /// <summary>
    ///     Marks <paramref name="boxed" />, the <c>Value</c> of a <see cref="ParameterBox{T}" />.
    /// </summary>
    /// <param name="boxed">The collection parameter, as the client substitutes it.</param>
    /// <returns>The marked collection parameter.</returns>
    public static Expression Mark(Expression boxed)
        => Call(MultipleParameters, boxed);

    /// <summary>
    ///     Replaces every mark in <paramref name="query" /> with EF's marker for the collection mode
    ///     <paramref name="context" /> is configured with.
    /// </summary>
    /// <param name="query">The query the server received.</param>
    /// <param name="context">The server's context.</param>
    /// <returns>The query, with each mark naming the server's mode.</returns>
    public static Expression ToServerMode(Expression query, DbContext context)
    {
        MethodInfo? marker = context.GetService<IDbContextOptions>().Extensions
                .OfType<RelationalOptionsExtension>()
                .FirstOrDefault()?.ParameterizedCollectionMode switch
            {
                ParameterTranslationMode.Constant => Constant,
                ParameterTranslationMode.Parameter => Parameter,
                _ => null,
            };

        // EF's default mode is what the mark already names, and a store that is not relational has
        // no collection modes at all.
        return marker is null ? query : new ServerModeRewriter(marker).Visit(query);
    }

    private static bool IsMark(MethodCallExpression node)
        => node.Method.IsGenericMethod
            && node.Method.GetGenericMethodDefinition() == MultipleParameters
            && node.Arguments[0] is MemberExpression { Expression: ConstantExpression { Value: { } box } }
            && box.GetType().IsGenericType
            && box.GetType().GetGenericTypeDefinition() == typeof(ParameterBox<>);

    private static MethodCallExpression Call(MethodInfo marker, Expression argument)
        => Expression.Call(marker.MakeGenericMethod(argument.Type), argument);

    private sealed class ServerModeRewriter(MethodInfo marker) : ExpressionVisitor
    {
        private readonly MethodInfo _marker = marker;

        protected override Expression VisitMethodCall(MethodCallExpression node)
            => IsMark(node) ? Call(_marker, node.Arguments[0]) : base.VisitMethodCall(node);
    }
}
