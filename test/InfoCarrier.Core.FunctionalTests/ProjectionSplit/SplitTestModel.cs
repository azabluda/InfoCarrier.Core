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
