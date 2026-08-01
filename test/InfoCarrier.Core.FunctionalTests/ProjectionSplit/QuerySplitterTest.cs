// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     How a captured query divides, and what the client does with the results
///     (<c>docs/projection-split.md</c> §3.4–§3.6, §6).
/// </summary>
public class QuerySplitterTest : IDisposable
{
    private readonly SplitTestContext _context = SplitTestContext.Create();
    private readonly QuerySplitter _splitter;

    private static readonly Author Austen = new() { Id = 1, Name = "Austen" };
    private static readonly Author Woolf = new() { Id = 2, Name = "Woolf" };

    public QuerySplitterTest()
    {
        _splitter = new QuerySplitter(_context.Model);

        Austen.Books = [new Book { Id = 1, Title = "Emma", AuthorId = 1 }];
        Woolf.Books = [];
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private SplitQuery Split(IQueryable query) => _splitter.Split(query.Expression);

    /// <summary>Stands in for the server: hands the residual the rows it would have returned.</summary>
    private static object? Run(SplitQuery split, params IEnumerable<object>[] rows)
        => split.Apply([.. rows.Select((r, i) => AsQueryable(r, split.ServerQueries[i].ElementType))]);

    private static object AsQueryable(IEnumerable<object> rows, Type elementType)
    {
        object typed = typeof(Enumerable).GetMethod(nameof(Enumerable.Cast))!
            .MakeGenericMethod(elementType).Invoke(null, [rows])!;
        return typeof(Queryable).GetMethod(nameof(Queryable.AsQueryable), 1, [typeof(IEnumerable<>).MakeGenericType(Type.MakeGenericMethodParameter(0))])!
            .MakeGenericMethod(elementType).Invoke(null, [typed])!;
    }

    [Fact]
    public void An_entity_query_is_pass_through()
    {
        SplitQuery split = Split(_context.Authors.Where(a => a.Id > 1));

        Assert.True(split.IsPassThrough);
        ServerQuery server = Assert.Single(split.ServerQueries);
        Assert.Equal(typeof(Author), server.ElementType);
        Assert.False(server.ReturnsSingleResult);
    }

    [Fact]
    public void A_single_result_query_is_marked_as_one()
    {
        SplitQuery split = _splitter.Split(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Count),
                [typeof(Author)],
                ((IQueryable<Author>)_context.Authors).Expression));

        Assert.True(Assert.Single(split.ServerQueries).ReturnsSingleResult);
    }

    [Fact]
    public void An_anonymous_projection_ships_entities_and_projects_on_the_client()
    {
        SplitQuery split = Split(_context.Authors.Select(a => new { a.Name }));

        Assert.False(split.IsPassThrough);
        Assert.Equal(typeof(Author), Assert.Single(split.ServerQueries).ElementType);

        object? result = Run(split, [Austen, Woolf]);
        Assert.Equal(["Austen", "Woolf"], ((IEnumerable<object>)result!).Select(x => x.GetType().GetProperty("Name")!.GetValue(x)));
    }

    [Fact]
    public void A_client_dto_projection_is_applied_on_the_client()
    {
        SplitQuery split = Split(_context.Authors.Select(a => new BookSummary { AuthorName = a.Name }));

        var result = (IEnumerable<BookSummary>)Run(split, [Austen, Woolf])!;
        Assert.Equal(["Austen", "Woolf"], result.Select(s => s.AuthorName));
    }

    [Fact]
    public void A_single_result_operator_after_a_client_projection_moves_to_the_client()
    {
        // The server ships a sequence; the residual takes the first. Reading
        // ReturnsSingleResult off the original query would have shipped a scalar instead.
        SplitQuery split = _splitter.Split(
            _context.Authors.Select(a => new { a.Name }).Take(1).Expression);

        Assert.False(Assert.Single(split.ServerQueries).ReturnsSingleResult);
        Assert.Single((IEnumerable<object>)Run(split, [Austen, Woolf])!);
    }

    [Fact]
    public void Filtering_after_a_client_projection_runs_on_the_client()
    {
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name }).Where(x => x.Name == "Woolf"));

        Assert.Single((IEnumerable<object>)Run(split, [Austen, Woolf])!);
    }

    [Fact]
    public void A_tracking_marker_is_stripped_from_the_residual()
    {
        // AsNoTracking has no Enumerable counterpart; leaving it in fails the rewriter.
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name }).AsNoTracking());

        Assert.Equal(2, ((IEnumerable<object>)Run(split, [Austen, Woolf])!).Count());
    }

    [Fact]
    public void A_navigation_read_on_the_client_adds_an_include_to_the_shipped_query()
    {
        // The silent-wrongness case: without the Include the server never loads Books and the
        // client answers 0 for every author.
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name, Count = a.Books.Count }));

        Expression shipped = Assert.Single(split.ServerQueries).Query;
        var call = Assert.IsAssignableFrom<MethodCallExpression>(shipped);
        Assert.Equal(nameof(EntityFrameworkQueryableExtensions.Include), call.Method.Name);
        Assert.Equal("Books", ((ConstantExpression)call.Arguments[1]).Value);

        object? result = Run(split, [Austen, Woolf]);
        Assert.Equal([1, 0], ((IEnumerable<object>)result!).Select(x => x.GetType().GetProperty("Count")!.GetValue(x)));
    }

    [Fact]
    public void A_chained_navigation_read_includes_the_whole_path()
    {
        SplitQuery split = Split(
            _context.Books.Select(b => new { b.Title, AuthorBooks = b.Author!.Books.Count }));

        var call = Assert.IsAssignableFrom<MethodCallExpression>(Assert.Single(split.ServerQueries).Query);
        Assert.Equal("Author.Books", ((ConstantExpression)call.Arguments[1]).Value);
    }

    [Fact]
    public void A_navigation_no_shipped_query_can_carry_is_rejected()
    {
        // Author rows are never shipped, so `.Books` would read an empty collection. Answering
        // 0 here is exactly what must not happen quietly.
        var query = _context.Books
            .Select(b => new { b.Title, Author = b.Author })
            .Select(x => new { x.Title, Count = x.Author!.Books.Count });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Split(query));
        Assert.Contains("Author.Books", ex.Message, StringComparison.Ordinal);
        Assert.Contains("silently", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_correlated_subquery_under_a_client_projection_is_rejected()
    {
        ParameterExpression a = Expression.Parameter(typeof(Author), "a");
        ParameterExpression b = Expression.Parameter(typeof(Book), "b");
        MethodCallExpression count = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.Count),
            [typeof(Book)],
            ((IQueryable<Book>)_context.Books).Expression,
            Expression.Quote(Expression.Lambda<Func<Book, bool>>(
                Expression.Equal(
                    Expression.Property(b, nameof(Book.AuthorId)),
                    Expression.Property(a, nameof(Author.Id))),
                b)));

        var selector = Expression.Lambda<Func<Author, AuthorSummary>>(
            Expression.MemberInit(
                Expression.New(typeof(AuthorSummary)),
                Expression.Bind(typeof(AuthorSummary).GetProperty(nameof(AuthorSummary.BookCount))!, count)),
            a);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Split(_context.Authors.Select(selector)));
        Assert.Contains("one query per row", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EF_Property_on_the_client_side_is_rejected()
    {
        var query = _context.Authors
            .Select(a => new { a.Name, Self = a })
            .Select(x => new { x.Name, Shadow = EF.Property<string>(x.Self, "Hidden") });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Split(query));
        Assert.Contains("EF.Property", ex.Message, StringComparison.Ordinal);
    }
}
