// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     What a candidate rewrite has to demonstrate before it is kept
///     (<c>docs/transparent-identifiers.md</c> §4).
/// </summary>
public class RewriteVerifierTest : IDisposable
{
    private readonly SplitTestContext _context = SplitTestContext.Create();
    private readonly RewriteVerifier _verifier;

    public RewriteVerifierTest()
        => _verifier = new RewriteVerifier(
            new ServerBoundaryAnalyzer(TypeAllowlist.ForModel(_context.Model)));

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     A projection through a client-only carrier and back out of it — two operators the
    ///     client has to run for a query the server could answer whole. The shape every rewrite
    ///     in this milestone is trying to remove.
    /// </summary>
    private IQueryable<string?> ThroughACarrier()
        => _context.Authors.Select(a => new { N = a.Name }).Select(x => x.N);

    [Fact]
    public void A_rewrite_that_moves_operators_to_the_server_is_kept()
    {
        RewriteVerdict verdict = _verifier.Verify(
            ThroughACarrier().Expression,
            _context.Authors.Select(a => a.Name).Expression);

        Assert.True(verdict.Accepted);
        Assert.Equal(RewriteRejection.None, verdict.Rejection);

        // Both operators crossed: the rewritten query ships whole.
        Assert.Equal(2, verdict.OriginalResidualOperators);
        Assert.Equal(0, verdict.CandidateResidualOperators);
        Assert.True(verdict.Analysis.IsWhollyServerExecutable);
    }

    [Fact]
    public void An_accepted_rewrite_is_what_the_caller_carries_on_with()
    {
        Expression candidate = _context.Authors.Select(a => a.Name).Expression;

        Assert.Same(candidate, _verifier.Verify(ThroughACarrier().Expression, candidate).Kept);
    }

    [Fact]
    public void A_rewrite_that_moves_nothing_is_discarded()
    {
        // Swapping one client-only carrier for another is a different tree and the same split:
        // still two operators on the client, still an anonymous type in the middle.
        RewriteVerdict verdict = _verifier.Verify(
            ThroughACarrier().Expression,
            _context.Authors
                .Select(a => new BookSummary { Title = a.Name })
                .Select(x => x.Title)
                .Expression);

        Assert.False(verdict.Accepted);
        Assert.Equal(RewriteRejection.NoGain, verdict.Rejection);
        Assert.Equal(verdict.OriginalResidualOperators, verdict.CandidateResidualOperators);
    }

    [Fact]
    public void A_discarded_rewrite_leaves_the_original_in_place()
    {
        Expression original = ThroughACarrier().Expression;

        RewriteVerdict verdict = _verifier.Verify(
            original,
            _context.Authors
                .Select(a => new BookSummary { Title = a.Name })
                .Select(x => x.Title)
                .Expression);

        Assert.Same(original, verdict.Kept);
        Assert.Same(original, verdict.Analysis.Root);
    }

    /// <summary>
    ///     The regression test for phase X1.
    /// </summary>
    /// <remarks>
    ///     Mirroring EF's <c>TryFlattenGroupJoinSelectMany</c> emits a join whose result selector
    ///     reconstructs the transparent identifier — grouping member and all — while the join
    ///     binds only the outer and inner elements. EF's own pipeline collapses that anonymous
    ///     type and drops the dead member before anything is compiled; this provider compiles the
    ///     residual and gets <c>variable 'g' … referenced from scope ''</c>, which names neither
    ///     the rewrite nor the query. The candidate below is that tree.
    /// </remarks>
    [Fact]
    public void A_rewrite_that_strands_a_parameter_is_discarded()
    {
        var a = Expression.Parameter(typeof(Author), "a");
        var b = Expression.Parameter(typeof(Book), "b");
        var grouping = Expression.Parameter(typeof(IEnumerable<Book>), "g");

        Type carrier = typeof(ValueTuple<Author, IEnumerable<Book>, Book>);
        Expression reconstructed = Expression.New(
            carrier.GetConstructor([typeof(Author), typeof(IEnumerable<Book>), typeof(Book)])!,
            a,
            grouping,
            b);

        Expression join = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Join),
            [typeof(Author), typeof(Book), typeof(int), carrier],
            _context.Authors.AsQueryable().Expression,
            _context.Books.AsQueryable().Expression,
            Expression.Lambda(Expression.Property(a, nameof(Author.Id)), a),
            Expression.Lambda(Expression.Property(b, nameof(Book.AuthorId)), b),
            Expression.Lambda(reconstructed, a, b));

        var t = Expression.Parameter(carrier, "t");
        Expression candidate = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [carrier, typeof(Book)],
            join,
            Expression.Lambda(Expression.Field(t, "Item3"), t));

        RewriteVerdict verdict = _verifier.Verify(
            _context.Authors.SelectMany(x => x.Books).Expression,
            candidate);

        Assert.False(verdict.Accepted);
        Assert.Equal(RewriteRejection.OpenTree, verdict.Rejection);
    }

    [Fact]
    public void A_rewrite_that_changes_the_result_type_is_discarded()
    {
        // This one would otherwise be accepted — it ships whole where the original ships nothing
        // — so only the type check can refuse it.
        RewriteVerdict verdict = _verifier.Verify(
            ThroughACarrier().Expression,
            _context.Authors.AsQueryable().Expression);

        Assert.False(verdict.Accepted);
        Assert.Equal(RewriteRejection.TypeChanged, verdict.Rejection);
        Assert.True(verdict.CandidateResidualOperators < verdict.OriginalResidualOperators);
    }

    [Fact]
    public void A_rewrite_that_creates_a_correlated_fragment_is_discarded()
    {
        // Built by hand for the same reason the analyzer's own tests are: written in C#,
        // `_context.Books` inside a lambda is a closure field read rather than a query root, and
        // EF's funcletizer has already rewritten it by the time the splitter sees the tree.
        var a = Expression.Parameter(typeof(Author), "a");
        var b = Expression.Parameter(typeof(Book), "b");

        MethodCallExpression correlatedTitle = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.FirstOrDefault),
            [typeof(string)],
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Select),
                [typeof(Book), typeof(string)],
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Where),
                    [typeof(Book)],
                    _context.Books.AsQueryable().Expression,
                    Expression.Lambda<Func<Book, bool>>(
                        Expression.Equal(
                            Expression.Property(b, nameof(Book.AuthorId)),
                            Expression.Property(a, nameof(Author.Id))),
                        b)),
                Expression.Lambda<Func<Book, string?>>(Expression.Property(b, nameof(Book.Title)), b)));

        // The subquery only becomes a *fragment* under a client-typed projection: above one that
        // ships, it would simply travel with it.
        Expression carried = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [typeof(Author), typeof(BookSummary)],
            _context.Authors.AsQueryable().Expression,
            Expression.Lambda<Func<Author, BookSummary>>(
                Expression.MemberInit(
                    Expression.New(typeof(BookSummary)),
                    Expression.Bind(typeof(BookSummary).GetProperty(nameof(BookSummary.Title))!, correlatedTitle)),
                a));

        var summary = Expression.Parameter(typeof(BookSummary), "x");
        Expression candidate = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Select),
            [typeof(BookSummary), typeof(string)],
            carried,
            Expression.Lambda(Expression.Property(summary, nameof(BookSummary.Title)), summary));

        RewriteVerdict verdict = _verifier.Verify(ThroughACarrier().Expression, candidate);

        Assert.False(verdict.Accepted);
        Assert.Equal(RewriteRejection.CorrelatedFragment, verdict.Rejection);
    }
}
