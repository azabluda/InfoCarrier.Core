// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     A two-entity model with a navigation in both directions — the smallest shape that can
///     express the cases the projection split turns on: a scalar read, a navigation read, and a
///     correlated subquery.
/// </summary>
public class Author
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public List<Book> Books { get; set; } = [];
}

public class Book
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public int AuthorId { get; set; }

    public Author? Author { get; set; }
}

/// <summary>
///     A client-only type: declared here, never in the model, so the server can never name it.
/// </summary>
public class BookSummary
{
    public string? Title { get; set; }

    public string? AuthorName { get; set; }
}

/// <summary>
///     A client-only carrier built by a constructor rather than an object initializer, holding an
///     entity alongside a scalar.
/// </summary>
/// <remarks>
///     The C# compiler fills in <see cref="System.Linq.Expressions.NewExpression.Members" /> only
///     for anonymous types, so this shape is one the carrier re-carry
///     ([ADR-011](../../../docs/decisions.md#adr-011)) deliberately leaves alone. That is what
///     makes it useful here: it keeps an entity on the client side of the boundary, which is the
///     situation the client-side halves of the split exist for and would otherwise be untestable
///     now that the anonymous-type version of the same query ships whole.
/// </remarks>
public class ClientRow(string? text, Author author)
{
    public string? Text { get; } = text;

    public Author Author { get; } = author;
}

/// <summary>
///     A client-only type with an <see cref="IQueryable{T}" /> member — EF's <c>QueryableDto</c>,
///     reproduced. A projection into one of these is what EF refuses outright ("Collections in
///     the final projection must be an <c>IEnumerable&lt;T&gt;</c> type"), so the split must not
///     quietly make it work by materializing on the way past.
/// </summary>
public class QueryableRow
{
    public IQueryable<Book>? Books { get; set; }
}

/// <summary>
///     A second client-only type, carrying a computed value rather than a copied one.
/// </summary>
public class AuthorSummary
{
    public string? Name { get; set; }

    public int BookCount { get; set; }
}

public class SplitTestContext(DbContextOptions<SplitTestContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();

    public DbSet<Book> Books => Set<Book>();

    /// <summary>
    ///     A context whose only job is to hand out real <c>EntityQueryRootExpression</c>s and a
    ///     real <c>IModel</c>; nothing is ever executed against it.
    /// </summary>
    public static SplitTestContext Create()
        => new(new DbContextOptionsBuilder<SplitTestContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
