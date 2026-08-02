// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     Allowlist cases the split depends on. A type wrongly denied here does not fail loudly at
///     the boundary — it moves the boundary, and the query silently does more work on the client.
/// </summary>
public class TypeAllowlistBoundaryTest : IDisposable
{
    private readonly SplitTestContext _context = SplitTestContext.Create();
    private readonly TypeAllowlist _allowlist;

    public TypeAllowlistBoundaryTest()
        => _allowlist = TypeAllowlist.ForModel(_context.Model);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_quoted_lambda_is_allowed()
    {
        // Every Queryable operator carries its lambda quoted, so denying this denies every
        // Where, Select and Include in the suite.
        Expression<Func<Author, IEnumerable<Book>>> include = a => a.Books;
        UnaryExpression quoted = Expression.Quote(include);

        Assert.True(
            _allowlist.IsAllowed(quoted.Type),
            $"Quote reported its type as '{quoted.Type}', which the allowlist denied.");
    }

    [Fact]
    public void An_expression_over_a_client_only_type_is_still_denied()
    {
        Expression<Func<Author, BookSummary>> projection = a => new BookSummary { AuthorName = a.Name };

        Assert.False(_allowlist.IsAllowed(Expression.Quote(projection).Type));
    }
}
