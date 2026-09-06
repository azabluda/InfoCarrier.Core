// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Names the operators in a split's residual that remove rows, so the
///     <see cref="InfoCarrierEventId.QuerySplit" /> event can say what stayed behind.
/// </summary>
/// <remarks>
///     <para>
///         <b>The distinction is between reshaping a row and dropping one.</b> A residual that
///         only reshapes what came back carried exactly the rows the caller asked for, and there
///         is nothing to report. A residual that drops rows means the server sent rows this
///         client threw away, and the caller has no other way to find that out: the answer is
///         correct either way, and the only symptom is the wire.
///     </para>
///     <para>
///         <b>The set below is the one an audit over the whole suite was written against</b>
///         (R173): 301 splits out of 29513 tests left one of these behind, every one of them in a
///         class that already had a decision. It is deliberately syntactic and deliberately
///         over-inclusive at the edges — an aggregate such as <c>Any</c> does not remove rows from
///         a result so much as reduce them to one answer, and it still means the server sent rows
///         to produce it.
///     </para>
///     <para>
///         <b>Walked only when something is listening.</b> A split is decided per execution rather
///         than per compilation, so a hot split query reaches this class on every execution;
///         <see cref="InfoCarrierLoggerExtensions" /> calls it after the log guards, never before.
///     </para>
/// </remarks>
internal static class RowRemovingOperators
{
    private static readonly HashSet<string> Names =
    [
        nameof(Queryable.All),
        nameof(Queryable.Any),
        nameof(Queryable.Contains),
        nameof(Queryable.Distinct),
        nameof(Queryable.DistinctBy),
        nameof(Queryable.ElementAt),
        nameof(Queryable.ElementAtOrDefault),
        nameof(Queryable.Except),
        nameof(Queryable.ExceptBy),
        nameof(Queryable.First),
        nameof(Queryable.FirstOrDefault),
        nameof(Queryable.Intersect),
        nameof(Queryable.IntersectBy),
        nameof(Queryable.Last),
        nameof(Queryable.LastOrDefault),
        nameof(Queryable.OfType),
        nameof(Queryable.Single),
        nameof(Queryable.SingleOrDefault),
        nameof(Queryable.Skip),
        nameof(Queryable.SkipWhile),
        nameof(Queryable.Take),
        nameof(Queryable.TakeWhile),
        nameof(Queryable.Where),
    ];

    /// <summary>
    ///     The sentence the split event ends with, for a residual that drops no rows.
    /// </summary>
    public const string NothingRemoved =
        "Everything left on this client reshapes rows, so the wire carried what the query asked for.";

    /// <summary>
    ///     Describes what the residual removes, in one sentence.
    /// </summary>
    /// <param name="residual">The part of the query this client runs.</param>
    /// <returns>
    ///     A sentence naming the row-removing operators, each once and the outermost first, or
    ///     <see cref="NothingRemoved" /> when there are none.
    /// </returns>
    public static string Describe(Expression residual)
    {
        var found = new List<string>();
        new Collector(found).Visit(residual);

        return found.Count == 0
            ? NothingRemoved
            : "These operators left on this client remove rows, so the server sent more than the "
                + "query asked for: " + string.Join(", ", found) + ".";
    }

    private sealed class Collector(List<string> found) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            ArgumentNullException.ThrowIfNull(node);

            if ((node.Method.DeclaringType == typeof(Queryable)
                    || node.Method.DeclaringType == typeof(Enumerable))
                && Names.Contains(node.Method.Name)
                && !found.Contains(node.Method.Name))
            {
                found.Add(node.Method.Name);
            }

            return base.VisitMethodCall(node);
        }
    }
}
