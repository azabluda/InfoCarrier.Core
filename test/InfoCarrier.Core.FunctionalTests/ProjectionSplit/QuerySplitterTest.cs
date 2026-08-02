// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using InfoCarrier.Core.Query;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     How a captured query divides, and whether both halves produce the right answer
///     (<c>docs/projection-split.md</c> §3.2–§3.6, §6).
/// </summary>
/// <remarks>
///     The shipped query is executed here against in-memory rows rather than asserted
///     structurally. Structural assertions were what let the first version of these tests pass
///     while the split answered <c>0</c>: the shape looked right and the value was wrong.
/// </remarks>
public class QuerySplitterTest : IDisposable
{
    private readonly SplitTestContext _context = SplitTestContext.Create();
    private readonly QuerySplitter _splitter;

    private readonly Author _austen = new() { Id = 1, Name = "Austen" };
    private readonly Author _woolf = new() { Id = 2, Name = "Woolf" };
    private readonly List<Author> _authors = [];
    private readonly List<Book> _books = [];

    public QuerySplitterTest()
    {
        _splitter = new QuerySplitter(_context.Model);

        var emma = new Book { Id = 1, Title = "Emma", AuthorId = 1, Author = _austen };
        _austen.Books = [emma];
        _woolf.Books = [];
        _authors.AddRange([_austen, _woolf]);
        _books.Add(emma);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private SplitQuery Split(IQueryable query) => _splitter.Split(query.Expression);

    /// <summary>
    ///     Runs the whole split: each shipped query against the in-memory rows, then the residual.
    /// </summary>
    private object? Run(SplitQuery split)
        => split.Apply([.. split.ServerQueries.Select(RunOnServer)]);

    private object? RunOnServer(ServerQuery serverQuery)
    {
        Expression bound = new ServerStandIn(_authors, _books).Visit(serverQuery.Query)!;
        return Expression.Lambda(bound).Compile().DynamicInvoke();
    }

    private static List<object?> Rows(object? result, string member)
        => [.. ((IEnumerable)result!).Cast<object>().Select(x => x.GetType().GetProperty(member)!.GetValue(x))];

    /// <summary>
    ///     Stands in for the server: binds query roots to local lists, and drops <c>Include</c>,
    ///     which LINQ-to-Objects has no counterpart for (the navigations are already populated).
    /// </summary>
    private sealed class ServerStandIn(List<Author> authors, List<Book> books) : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
            => node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression root
                ? Expression.Constant(
                    root.ElementType == typeof(Author) ? authors.AsQueryable() : books.AsQueryable())
                : base.VisitExtension(node);

        protected override Expression VisitMethodCall(MethodCallExpression node)
            => node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions)
                && node.Method.Name == nameof(EntityFrameworkQueryableExtensions.Include)
                    ? Visit(node.Arguments[0])
                    : base.VisitMethodCall(node);
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
        Assert.Equal(2, Run(split));
    }

    [Fact]
    public void A_projection_ships_only_the_values_it_needs()
    {
        // Wire-protocol W1: one string per row, not a Customer. The projection rewrite and the
        // minimal-column payload are the same mechanism (§3.2).
        SplitQuery split = Split(_context.Authors.Select(a => new { a.Name }));

        Assert.Equal(typeof(ValueTuple<string>), Assert.Single(split.ServerQueries).ElementType);
        Assert.Equal(["Austen", "Woolf"], Rows(Run(split), "Name"));
    }

    [Fact]
    public void A_client_dto_projection_is_applied_on_the_client()
    {
        SplitQuery split = Split(_context.Authors.Select(a => new BookSummary { AuthorName = a.Name }));

        var result = (IEnumerable<BookSummary>)Run(split)!;
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
        Assert.Single((IEnumerable)Run(split)!);
    }

    [Fact]
    public void Filtering_after_a_client_projection_runs_on_the_client()
    {
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name }).Where(x => x.Name == "Woolf"));

        Assert.Equal(["Woolf"], Rows(Run(split), "Name"));
    }

    [Fact]
    public void A_tracking_marker_is_stripped_from_the_residual()
    {
        // AsNoTracking has no Enumerable counterpart; leaving it in fails the rewriter.
        SplitQuery split = Split(_context.Authors.Select(a => new { a.Name }).AsNoTracking());

        Assert.Equal(["Austen", "Woolf"], Rows(Run(split), "Name"));
    }

    [Fact]
    public void A_navigation_read_is_evaluated_on_the_server()
    {
        // The silent-wrongness case. Cut at the projection, the client reads Books on an entity
        // whose collection was never loaded and answers 0 for every author.
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name, Count = a.Books.Count }));

        Assert.Equal(typeof(ValueTuple<string, int>), Assert.Single(split.ServerQueries).ElementType);
        Assert.Equal([1, 0], Rows(Run(split), "Count"));
    }

    [Fact]
    public void A_chained_navigation_read_is_evaluated_on_the_server()
    {
        SplitQuery split = Split(
            _context.Books.Select(b => new { b.Title, AuthorBooks = b.Author!.Books.Count }));

        Assert.Equal([1], Rows(Run(split), "AuthorBooks"));
    }

    [Fact]
    public void A_correlated_subquery_under_a_client_projection_is_evaluated_on_the_server()
    {
        // Built by hand because the C# compiler emits `_context.Books` as a closure field read,
        // not a query root; EF's funcletizer rewrites it before the ADR-006 capture point.
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
                Expression.Bind(typeof(AuthorSummary).GetProperty(nameof(AuthorSummary.Name))!,
                    Expression.Property(a, nameof(Author.Name))),
                Expression.Bind(typeof(AuthorSummary).GetProperty(nameof(AuthorSummary.BookCount))!, count)),
            a);

        SplitQuery split = Split(_context.Authors.Select(selector));

        // One query, not one per row: the subquery travels inside the row projection.
        Assert.Single(split.ServerQueries);
        var result = (IEnumerable<AuthorSummary>)Run(split)!;
        Assert.Equal([1, 0], result.Select(s => s.BookCount));
    }

    [Fact]
    public void A_join_with_a_client_result_selector_stays_one_server_query()
    {
        // Cut, this became two shipped queries joined again on the client. Rewriting the result
        // selector leaves the join where it belongs and ships one tuple per matched pair.
        SplitQuery split = Split(
            _context.Authors.Join(
                _context.Books,
                a => a.Id,
                b => b.AuthorId,
                (a, b) => new { a.Name, b.Title }));

        ServerQuery server = Assert.Single(split.ServerQueries);
        Assert.Equal(typeof(ValueTuple<string, string>), server.ElementType);
        Assert.Equal(["Austen"], Rows(Run(split), "Name"));
        Assert.Equal(["Emma"], Rows(Run(split), "Title"));
    }

    [Fact]
    public void A_group_join_with_default_if_empty_flattens_into_a_left_join()
    {
        // The transparent identifier here holds the *grouping* — a sequence, which the carrier
        // re-carry must refuse to put in a tuple slot. Flattening removes it entirely, which is
        // the only way this shape reaches the server: left alone, the client applies
        // `DefaultIfEmpty` with LINQ-to-Objects semantics and dereferences the null row that SQL
        // would have answered with nulls throughout.
        SplitQuery split = Split(
            from a in _context.Authors
            join b in _context.Books on a.Id equals b.AuthorId into g
            from b in g.DefaultIfEmpty()
            select new { a.Name, Title = b == null ? null : b.Title });

        Expression shipped = Assert.Single(split.ServerQueries).Query;
        Assert.Contains(nameof(Queryable.LeftJoin), Operators(shipped));
        Assert.DoesNotContain(nameof(Queryable.GroupJoin), Operators(shipped));

        Assert.Equal(["Austen", "Woolf"], Rows(Run(split), "Name"));
        Assert.Equal(["Emma", null], Rows(Run(split), "Title"));
    }

    [Fact]
    public void A_group_join_whose_identifier_survives_is_left_alone()
    {
        // `from b in g` with clauses after it keeps the transparent identifier, so substituting
        // the group-join result selector reconstructs it — grouping member and all — and the
        // flattened join would name a parameter it does not bind. EF's pipeline drops that dead
        // member before anything compiles; this one compiles the residual, so the rewrite has to
        // decline and the query still has to answer.
        SplitQuery split = Split(
            from a in _context.Authors
            join b in _context.Books on a.Id equals b.AuthorId into g
            from b in g
            where b.Title != null
            select b.Title);

        Assert.Equal(["Emma"], (IEnumerable<string?>)Run(split)!);
    }

    private static List<string> Operators(Expression expression)
    {
        List<string> names = [];
        new OperatorCollector(names).Visit(expression);
        return names;
    }

    private sealed class OperatorCollector(ICollection<string> names) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            names.Add(node.Method.Name);
            return base.VisitMethodCall(node);
        }
    }

    [Fact]
    public void A_collection_the_projection_composes_over_ships_materialized()
    {
        // EF refuses a final projection that returns an IQueryable: "Collections in the final
        // projection must be an IEnumerable<T> type such as List<T>". The largest
        // server-evaluable fragment here is the queryable `Where`, so putting it in a slot
        // verbatim ships exactly the projection EF rejects — and the failure names the
        // projection, not this rewrite.
        // The inner projection has to be client-typed, or the whole body is server-ok and there
        // is nothing to rewrite — `Select(b => b.Title)` returns a `List<string>`, which the
        // server can name perfectly well.
        SplitQuery split = Split(
            _context.Authors.Select(a =>
                a.Books.AsQueryable().Where(b => b.Title != null).Select(b => new { b.Title }).ToList()));

        // Nested, because the inner projection was rewritten first: only the Title travels, not
        // whole Book rows (wire-protocol W1), and the materialization wraps that.
        Assert.Equal(
            typeof(ValueTuple<List<ValueTuple<string>>>),
            Assert.Single(split.ServerQueries).ElementType);

        List<object?> rows = [.. ((IEnumerable)Run(split)!).Cast<object?>()];
        Assert.Equal(2, rows.Count);
        Assert.Equal(["Emma"], ((IEnumerable)rows[0]!).Cast<object>()
            .Select(x => x.GetType().GetProperty("Title")!.GetValue(x)));
        Assert.Empty((IEnumerable)rows[1]!);
    }

    [Fact]
    public void A_collection_the_projection_returns_is_left_for_EF_to_refuse()
    {
        // The caller declared the member `IQueryable<Book>`, so the queryable *is* the answer
        // rather than an intermediate. Materializing it here would suppress an error EF is
        // supposed to raise — two spec tests assert exactly that error.
        SplitQuery split = Split(
            _context.Authors.Select(a => new QueryableRow { Books = a.Books.AsQueryable() }));

        Assert.Equal(
            typeof(ValueTuple<IQueryable<Book>>),
            Assert.Single(split.ServerQueries).ElementType);
    }

    [Fact]
    public void A_collection_projection_reassembles_above_the_SelectMany()
    {
        // Rewritten in place, the client-side reassembly would sit inside the collection
        // selector, which makes the whole SelectMany client-side — so the source ships alone and
        // the residual reads Books off authors that never carried them. Hoisting it above the
        // SelectMany is what lets the join ship.
        //
        // `a.Name` is the part that makes this more than a move: after the hoist the outer row
        // is out of scope, so it has to travel in a slot of its own.
        SplitQuery split = Split(
            _context.Authors.SelectMany(a => a.Books.Select(b => new
            {
                Client = Threshold(b.Id),
                b.Title,
                AuthorName = a.Name,
            })));

        Assert.Equal(
            typeof(ValueTuple<int, string, string>),
            Assert.Single(split.ServerQueries).ElementType);

        Assert.Equal([true], Rows(Run(split), "Client"));
        Assert.Equal(["Emma"], Rows(Run(split), "Title"));
        Assert.Equal(["Austen"], Rows(Run(split), "AuthorName"));
    }

    [Fact]
    public void A_group_by_stays_composed_with_its_aggregate()
    {
        // The cut's own doing: separating GroupBy from the aggregate that consumes it leaves a
        // bare non-composed GroupBy, which no provider can translate. The original query is fine.
        SplitQuery split = Split(
            _context.Books.GroupBy(b => b.AuthorId).Select(g => new { Id = g.Key, Count = g.Count() }));

        // The aggregate Select travels *with* the GroupBy — that composition is the whole point.
        var call = Assert.IsAssignableFrom<MethodCallExpression>(Assert.Single(split.ServerQueries).Query);
        Assert.Equal(nameof(Queryable.Select), call.Method.Name);
        Assert.Equal(
            nameof(Queryable.GroupBy),
            Assert.IsAssignableFrom<MethodCallExpression>(call.Arguments[0]).Method.Name);
        Assert.Equal([1], Rows(Run(split), "Count"));
    }

    [Fact]
    public void An_ordering_key_is_not_mistaken_for_a_projection()
    {
        // OrderBy<TSource, TKey> also ends in a lambda returning its last generic argument.
        // Rewriting it would replace the rows with their sort keys.
        SplitQuery split = Split(_context.Authors.OrderBy(a => a.Name));

        Assert.True(split.IsPassThrough);
        Assert.Equal(typeof(Author), Assert.Single(split.ServerQueries).ElementType);
    }

    [Fact]
    public void A_client_method_in_a_predicate_is_a_translation_failure()
    {
        // Not a boundary: the predicate calls a method the server cannot run. Answering it by
        // fetching every author and filtering locally is what EF's relational providers refuse
        // to do, and so does this one.
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Split(_context.Authors.Where(a => Threshold(a.Books.Count))));

        Assert.Contains("could not be translated", ex.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Threshold), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_navigation_no_shipped_query_can_carry_is_rejected()
    {
        // The entity escapes into a client type, and the navigation is read a step later — past
        // the point the projection rewrite can reach. Answering 0 here is what must not happen.
        var query = _context.Books
            .Select(b => new ClientRow(b.Title, b.Author!))
            .Select(x => new { x.Text, Count = x.Author.Books.Count });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Split(query));
        Assert.Contains("Books", ex.Message, StringComparison.Ordinal);
        Assert.Contains("silently", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_carrier_the_query_creates_and_consumes_is_re_carried_as_a_tuple()
    {
        // The transparent identifier the compiler puts between the two `from` clauses. Nothing
        // observes it, so replacing it with a tuple leaves the whole query on the server —
        // which is the entire point of ADR-011.
        //
        // Also the regression test for the delegate type: `SelectMany`'s collection selector is
        // declared `Func<T, IEnumerable<Book>>` while `a.Books` is a `List<Book>`. Re-inferring
        // the delegate from the body narrows it, the rebuilt call stops matching the operator,
        // and the rewrite is silently discarded — this query goes back to being split.
        SplitQuery split = Split(
            from a in _context.Authors
            from b in a.Books
            where b.Title != null
            select a);

        Assert.True(split.IsPassThrough);
        Assert.Equal(["Austen"], ((IEnumerable<Author>)Run(split)!).Select(a => a.Name));
    }

    [Fact]
    public void A_carrier_compared_to_null_is_carried_by_a_reference_tuple()
    {
        // The carrier is built inside a *predicate*, so it never crosses the wire — but the
        // server still has to construct it, which makes it a boundary like any other. A
        // `ValueTuple` cannot serve here: comparing a struct to null is not an expression that
        // can be built at all, so the rewrite throws, is discarded, and the predicate stays on
        // the client — where LINQ-to-Objects applies `First` strictly and throws on an empty
        // sequence that SQL would answer with null.
        SplitQuery split = Split(
            _context.Authors.Where(a => a.Books.Select(b => new { b.Title }).FirstOrDefault() == null));

        Assert.True(split.IsPassThrough);
        Assert.Equal(["Woolf"], ((IEnumerable<Author>)Run(split)!).Select(a => a.Name));
    }

    [Fact]
    public void A_returned_carrier_is_re_carried_when_that_frees_a_correlated_subquery()
    {
        // The projection sits inside a collection selector and reads the *outer* row, so
        // rewriting it in place leaves a server-side fragment that still references `a`. Carrying
        // the returned type as a tuple and rebuilding it once at the root instead lets the whole
        // `SelectMany` ship, which is the only placement that does not either strand the
        // fragment or issue one query per row.
        SplitQuery split = Split(
            from a in _context.Authors
            from x in a.Books.Select(b => new { b.Title, AuthorName = a.Name })
            select x);

        Assert.Equal(["Emma"], Rows(Run(split), "Title"));
        Assert.Equal(["Austen"], Rows(Run(split), "AuthorName"));
    }

    [Fact]
    public void A_returned_carrier_that_can_be_absent_stays_absent()
    {
        // `FirstOrDefault` over a `ValueTuple` answers `(null)` — a row that looks real — where
        // the anonymous type answered `null`. The carrier has to be a reference type, and the
        // rebuild has to pass the absence through rather than read slots out of it.
        SplitQuery split = Split(
            _context.Authors.Select(a => a.Books.Select(b => new { b.Title }).FirstOrDefault()));

        List<object?> rows = [.. ((IEnumerable)Run(split)!).Cast<object?>()];

        Assert.Equal(2, rows.Count);
        Assert.Equal("Emma", rows[0]!.GetType().GetProperty("Title")!.GetValue(rows[0]));
        Assert.Null(rows[1]);
    }

    [Fact]
    public void A_carrier_the_query_returns_is_left_alone()
    {
        // The caller asked for this type, so it is not plumbing. Re-carrying it would hand back
        // a tuple where an anonymous type was requested — the projection rewrite is what handles
        // these, by rebuilding the type on the client.
        SplitQuery split = Split(_context.Authors.Select(a => new { a.Name }).Where(x => x.Name != null));

        Assert.Equal(["Austen", "Woolf"], Rows(Run(split), "Name"));
    }

    [Fact]
    public void A_carrier_holding_a_sequence_is_left_alone()
    {
        // The guard the reassembly deferral violated: a slot holding a sequence asks SQL to
        // navigate out of a projected tuple back into a correlated collection.
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name, Books = a.Books }).Select(x => x.Name));

        Assert.False(split.IsPassThrough);
        Assert.Equal(["Austen", "Woolf"], (IEnumerable<string?>)Run(split)!);
    }

    [Fact]
    public void A_carrier_that_escapes_through_a_cast_is_left_alone()
    {
        // `Cast<object>` hides the carrier from the query's result type while the value still
        // reaches the caller, so "does it reach the result type" is not on its own enough.
        SplitQuery split = Split(
            _context.Authors.Select(a => new { a.Name, Self = a }).Cast<object>());

        Assert.Equal(["Austen", "Woolf"], Rows(Run(split), "Name"));
    }

    [Fact]
    public void The_same_query_through_an_anonymous_carrier_is_answered_on_the_server()
    {
        // The shape above, written the way the C# compiler writes a transparent identifier. The
        // carrier is created and consumed inside the query, so ADR-011 re-carries it as a tuple,
        // the navigation read goes to the server with everything else, and the refusal is never
        // needed. This is the improvement the refusal above is the fallback for — asserted on the
        // value, because the failure it replaced was a plausible wrong number rather than a throw.
        var query = _context.Books
            .Select(b => new { b.Title, Author = b.Author })
            .Select(x => new { x.Title, Count = x.Author!.Books.Count });

        Assert.Equal([1], Rows(Run(Split(query)), "Count"));
    }

    [Fact]
    public void EF_Property_on_the_client_side_reads_through_the_model()
    {
        // EF.Property has no runtime body, but the value is on the materialized entity and the
        // model knows how to read it.
        var query = _context.Authors
            .Select(a => new ClientRow(a.Name, a))
            .Select(x => new { x.Text, Id = EF.Property<int>(x.Author, nameof(Author.Id)) });

        Assert.Equal([1, 2], Rows(Run(Split(query)), "Id"));
    }

    [Fact]
    public void EF_Property_for_a_shadow_property_says_why_it_cannot()
    {
        var query = _context.Authors
            .Select(a => new ClientRow(a.Name, a))
            .Select(x => new { x.Text, Shadow = EF.Property<string>(x.Author, "Hidden") });

        // The residual is lazy, so the read only happens when the rows are pulled.
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => Rows(Run(Split(query)), "Shadow"));
        Assert.Contains("Hidden", ex.Message, StringComparison.Ordinal);
    }

    private static bool Threshold(int count) => count > 0;
}
