// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Decides whether a candidate rewrite of a captured query is worth keeping
///     (<c>docs/transparent-identifiers.md</c> §4,
///     [ADR-011](../../../docs/decisions.md#adr-011)).
/// </summary>
/// <remarks>
///     <para>
///         A rewrite that moves the type boundary is a guess: it is easy to build a tree the
///         expression API accepts and the query pipeline cannot use. The first attempt at one
///         committed to its output blindly and could only be assessed by running four thousand
///         tests, which cost a 91 → 383 regression to discover. This inverts that. A rewrite has
///         to <em>demonstrate</em> that it ships strictly more of the query than the tree it
///         replaces, or it is discarded and the original stands.
///     </para>
///     <para>
///         So the worst a guarded rewrite can do is fail to help. That is a much weaker claim
///         than "the rewrite is correct" — see <see cref="RewriteRejection" /> for what is
///         actually checked, and §4 of the spec for what deliberately is not.
///     </para>
/// </remarks>
public sealed class RewriteVerifier
{
    private readonly ServerBoundaryAnalyzer _analyzer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RewriteVerifier" /> class.
    /// </summary>
    /// <param name="analyzer">
    ///     The same analyzer the split uses, so a rewrite is judged by the boundary that will
    ///     actually be applied to it rather than by a second opinion.
    /// </param>
    public RewriteVerifier(ServerBoundaryAnalyzer analyzer)
        => _analyzer = analyzer;

    /// <summary>
    ///     Judges <paramref name="candidate" /> against the tree it would replace.
    /// </summary>
    public RewriteVerdict Verify(Expression original, Expression candidate)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(candidate);

        BoundaryAnalysis before = _analyzer.Analyze(original);
        BoundaryAnalysis after = _analyzer.Analyze(candidate);

        int beforeCount = ResidualOperatorCount(before);
        int afterCount = ResidualOperatorCount(after);

        RewriteRejection rejection = Judge(original, candidate, before, after, beforeCount, afterCount);

        return new RewriteVerdict(
            rejection,
            rejection == RewriteRejection.None ? candidate : original,
            rejection == RewriteRejection.None ? after : before,
            beforeCount,
            afterCount);
    }

    private static RewriteRejection Judge(
        Expression original,
        Expression candidate,
        BoundaryAnalysis before,
        BoundaryAnalysis after,
        int beforeCount,
        int afterCount)
    {
        // Whatever the rewrite does inside the tree, the tree still has to be the same query as
        // far as its caller is concerned. A changed root type means the residual lambda and the
        // caller's expectations no longer agree, and that shows up far from the cause.
        if (candidate.Type != original.Type && !IsSameSequence(original.Type, candidate.Type))
        {
            return RewriteRejection.TypeChanged;
        }

        // The check phase X1 needed and did not have. Substituting one subtree for another can
        // strand a parameter whose binder was rewritten away; the analyzer already tracks free
        // parameters per node, so the root's set answers this directly. Left unchecked it
        // surfaces as "variable 'orders' … referenced from scope ''" at compile time, which
        // names neither the rewrite nor the query.
        if (!after.FactsFor(candidate).IsClosed)
        {
            return RewriteRejection.OpenTree;
        }

        // A subtree that was shippable and is now a correlated fragment is not an improvement
        // even when it looks like one: the split refuses those outright, so the rewrite would
        // turn a working query into an exception.
        if (after.OpenFragments.Count > before.OpenFragments.Count)
        {
            return RewriteRejection.CorrelatedFragment;
        }

        return afterCount < beforeCount ? RewriteRejection.None : RewriteRejection.NoGain;
    }

    /// <summary>
    ///     Whether two root types are the same sequence for a caller's purposes: both queryable,
    ///     same element type.
    /// </summary>
    /// <remarks>
    ///     The only difference this admits is the queryable *interface*. A root rebuild appends a
    ///     <c>Select</c>, which answers <see cref="IQueryable{T}" /> where a query ending in
    ///     <c>OrderBy(…).ThenBy(…)</c> was an <see cref="IOrderedQueryable{T}" /> — and the strict
    ///     type check then rejected an otherwise good rewrite as <see cref="RewriteRejection.TypeChanged" />.
    ///     Nothing composes above the root of a captured query, so the one thing
    ///     <see cref="IOrderedQueryable{T}" /> buys there — a <c>ThenBy</c> on top — has no caller;
    ///     the *element* type is what the residual lambda and <c>ExecuteQuery&lt;TElement&gt;</c>
    ///     agree on, and that must still match exactly.
    /// </remarks>
    private static bool IsSameSequence(Type original, Type candidate)
        => typeof(IQueryable).IsAssignableFrom(original)
            && typeof(IQueryable).IsAssignableFrom(candidate)
            && ServerBoundaryAnalyzer.ElementTypeOf(original) == ServerBoundaryAnalyzer.ElementTypeOf(candidate);

    /// <summary>
    ///     How many query operators the client would still have to run.
    /// </summary>
    /// <remarks>
    ///     This is the quantity the whole exercise is about — moving the boundary means moving
    ///     operators across it — and it is the one measure that stays comparable across a rewrite.
    ///     Node counts do not: replacing an anonymous type with a <see cref="ValueTuple" /> changes
    ///     the tree's size without moving anything. Operators do not appear or disappear under
    ///     these rewrites, they only change sides.
    /// </remarks>
    private static int ResidualOperatorCount(BoundaryAnalysis analysis)
    {
        var serverSide = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        foreach (Expression subtree in analysis.Shippable)
        {
            new NodeCollector(serverSide).Visit(subtree);
        }

        var counter = new OperatorCounter(serverSide);
        counter.Visit(analysis.Root);
        return counter.Count;
    }

    private static bool IsQueryOperator(MethodCallExpression node)
        => node.Method.DeclaringType is { } declaring
            && (declaring == typeof(Queryable)
                || declaring == typeof(Enumerable)
                || declaring == typeof(EntityFrameworkQueryableExtensions));

    private sealed class NodeCollector(ISet<Expression> into) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is not null)
            {
                into.Add(node);
            }

            return base.Visit(node);
        }
    }

    private sealed class OperatorCounter(IReadOnlySet<Expression> serverSide) : ExpressionVisitor
    {
        public int Count { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (serverSide.Contains(node))
            {
                // Everything below a shipped subtree is shipped with it, so there is nothing
                // left to count in there.
                return node;
            }

            if (IsQueryOperator(node))
            {
                Count++;
            }

            return base.VisitMethodCall(node);
        }
    }
}

/// <summary>
///     Why a candidate rewrite was discarded.
/// </summary>
public enum RewriteRejection
{
    /// <summary>
    ///     It was not — the candidate ships strictly more of the query and is well formed.
    /// </summary>
    None = 0,

    /// <summary>
    ///     The candidate's root type differs from the original's, so it is not a replacement for
    ///     it whatever else it does.
    /// </summary>
    TypeChanged,

    /// <summary>
    ///     The candidate refers to a parameter nothing binds — a binder rewritten away while a
    ///     reference to it survived.
    /// </summary>
    OpenTree,

    /// <summary>
    ///     The candidate turns a shippable subtree into a correlated subquery under a client-side
    ///     projection, which the split refuses rather than issue one query per row.
    /// </summary>
    CorrelatedFragment,

    /// <summary>
    ///     The candidate is well formed but leaves as much on the client as the original did. A
    ///     rewrite has to pay for itself; "no worse" is not a reason to prefer a rewritten tree
    ///     to the one the caller wrote.
    /// </summary>
    NoGain,
}

/// <summary>
///     The result of <see cref="RewriteVerifier.Verify" />.
/// </summary>
/// <param name="Rejection">Why the candidate was discarded, or <see cref="RewriteRejection.None" />.</param>
/// <param name="Kept">The tree to carry on with — the candidate if accepted, else the original.</param>
/// <param name="Analysis">The boundary analysis of <paramref name="Kept" />, so callers need not re-run it.</param>
/// <param name="OriginalResidualOperators">Query operators the original left on the client.</param>
/// <param name="CandidateResidualOperators">Query operators the candidate would leave on the client.</param>
public sealed record RewriteVerdict(
    RewriteRejection Rejection,
    Expression Kept,
    BoundaryAnalysis Analysis,
    int OriginalResidualOperators,
    int CandidateResidualOperators)
{
    /// <summary>
    ///     Whether the candidate was kept.
    /// </summary>
    public bool Accepted => Rejection == RewriteRejection.None;
}
