// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     Where the analyzer places the boundary (<c>docs/projection-split.md</c> §3.1, §3.5).
/// </summary>
public class ServerBoundaryAnalyzerTest : IDisposable
{
    private readonly SplitTestContext _context = SplitTestContext.Create();
    private readonly ServerBoundaryAnalyzer _analyzer;

    public ServerBoundaryAnalyzerTest()
        => _analyzer = new ServerBoundaryAnalyzer(TypeAllowlist.ForModel(_context.Model));

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private BoundaryAnalysis Analyze(IQueryable query)
        => _analyzer.Analyze(query.Expression);

    [Fact]
    public void A_query_of_entities_ships_whole()
    {
        BoundaryAnalysis analysis = Analyze(_context.Authors.Where(a => a.Name == "x"));

        Assert.True(analysis.IsWhollyServerExecutable);
        Assert.Empty(analysis.OpenFragments);
    }

    [Fact]
    public void A_projection_to_a_known_type_ships_whole()
    {
        // string is server-known, so there is no boundary — this must not be split.
        Assert.True(Analyze(_context.Authors.Select(a => a.Name)).IsWhollyServerExecutable);
    }

    [Fact]
    public void An_ordered_paged_entity_query_ships_whole()
    {
        Assert.True(Analyze(
            _context.Authors.OrderBy(a => a.Name).Skip(5).Take(10)).IsWhollyServerExecutable);
    }

    [Fact]
    public void An_anonymous_projection_stops_at_the_source()
    {
        BoundaryAnalysis analysis = Analyze(_context.Authors.Select(a => new { a.Name }));

        Assert.False(analysis.IsWhollyServerExecutable);
        Expression shipped = Assert.Single(analysis.Shippable);
        Assert.Equal(typeof(IQueryable<Author>), shipped.Type);
        Assert.Empty(analysis.OpenFragments);
    }

    [Fact]
    public void A_client_only_dto_projection_stops_at_the_source()
    {
        BoundaryAnalysis analysis = Analyze(
            _context.Books.Select(b => new BookSummary { Title = b.Title }));

        Expression shipped = Assert.Single(analysis.Shippable);
        Assert.Equal(typeof(IQueryable<Book>), shipped.Type);
    }

    [Fact]
    public void Server_side_filtering_before_a_client_projection_still_ships()
    {
        // The Where is server-ok and sits below the boundary, so it must travel with the source
        // rather than be dragged onto the client.
        BoundaryAnalysis analysis = Analyze(
            _context.Authors.Where(a => a.Id > 3).Select(a => new { a.Name }));

        Expression shipped = Assert.Single(analysis.Shippable);
        var call = Assert.IsAssignableFrom<MethodCallExpression>(shipped);
        Assert.Equal(nameof(Queryable.Where), call.Method.Name);
    }

    [Fact]
    public void Filtering_after_a_client_projection_stays_on_the_client()
    {
        BoundaryAnalysis analysis = Analyze(
            _context.Authors.Select(a => new { a.Name }).Where(x => x.Name == "a"));

        Expression shipped = Assert.Single(analysis.Shippable);
        Assert.Equal(typeof(IQueryable<Author>), shipped.Type);
    }

    [Fact]
    public void A_join_with_an_anonymous_result_ships_both_sources()
    {
        BoundaryAnalysis analysis = Analyze(
            _context.Authors.Join(
                _context.Books,
                a => a.Id,
                b => b.AuthorId,
                (a, b) => new { a.Name, b.Title }));

        Assert.Equal(2, analysis.Shippable.Count);
        Assert.Contains(analysis.Shippable, e => e.Type == typeof(IQueryable<Author>));
        Assert.Contains(analysis.Shippable, e => e.Type == typeof(IQueryable<Book>));
    }

    [Fact]
    public void A_correlated_subquery_under_a_client_projection_is_reported_open()
    {
        // Server-executable in isolation, but it reads `a` from the enclosing lambda. A cut
        // cannot place it: evaluating it on the client would re-query the server once per row.
        //
        // Built by hand because the C# compiler emits `_context.Books` as a closure field read,
        // not a query root. EF's funcletizer rewrites that to an EntityQueryRootExpression before
        // the ADR-006 capture point, so this is the shape the splitter actually sees — writing it
        // in C# would test the compiler's closure, not the analyzer.
        ParameterExpression a = Expression.Parameter(typeof(Author), "a");
        ParameterExpression b = Expression.Parameter(typeof(Book), "b");
        var correlated = Expression.Lambda<Func<Book, bool>>(
            Expression.Equal(
                Expression.Property(b, nameof(Book.AuthorId)),
                Expression.Property(a, nameof(Author.Id))),
            b);
        MethodCallExpression count = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Count),
            [typeof(Book)],
            ((IQueryable<Book>)_context.Books).Expression,
            Expression.Quote(correlated));

        var selector = Expression.Lambda<Func<Author, AuthorSummary>>(
            Expression.MemberInit(
                Expression.New(typeof(AuthorSummary)),
                Expression.Bind(typeof(AuthorSummary).GetProperty(nameof(AuthorSummary.BookCount))!, count)),
            a);

        BoundaryAnalysis analysis = Analyze(_context.Authors.Select(selector));

        Expression open = Assert.Single(analysis.OpenFragments);
        var call = Assert.IsAssignableFrom<MethodCallExpression>(open);
        Assert.Equal(nameof(Queryable.Count), call.Method.Name);
        Assert.Single(analysis.Shippable);
    }

    [Fact]
    public void A_navigation_read_under_a_client_projection_is_not_open()
    {
        // `a.Books` is a member access, not a query root — nothing to ship, and nothing to
        // report. It is the A4 Include case, and the reason A3 alone would answer 0.
        BoundaryAnalysis analysis = Analyze(
            _context.Authors.Select(a => new { a.Name, Count = a.Books.Count }));

        Assert.Empty(analysis.OpenFragments);
        Assert.Single(analysis.Shippable);
    }

    [Fact]
    public void An_unserializable_node_kind_is_never_server_ok()
    {
        ParameterExpression a = Expression.Parameter(typeof(Author), "a");
        Expression<Func<Author, bool>> predicate = Expression.Lambda<Func<Author, bool>>(
            Expression.Block(Expression.Constant(true)), a);

        BoundaryAnalysis analysis = Analyze(_context.Authors.Where(predicate));

        // The Where cannot travel; its source still can.
        Assert.False(analysis.IsWhollyServerExecutable);
        Expression shipped = Assert.Single(analysis.Shippable);
        Assert.Equal(typeof(IQueryable<Author>), shipped.Type);
    }

    [Fact]
    public void Facts_report_free_parameters()
    {
        Expression<Func<Author, string?>> lambda = a => a.Name;
        BoundaryAnalysis analysis = _analyzer.Analyze(lambda);

        Assert.True(analysis.FactsFor(lambda).IsClosed);
        Assert.False(analysis.FactsFor(lambda.Body).IsClosed);
        Assert.Same(lambda.Parameters[0], Assert.Single(analysis.FactsFor(lambda.Body).Free));
    }
}
