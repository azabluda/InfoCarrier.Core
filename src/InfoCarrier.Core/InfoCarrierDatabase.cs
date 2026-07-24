// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core;

/// <summary>
///     The InfoCarrier client provider's <see cref="IDatabase" />: the raw-capture query entry
///     point (ADR-006). <see cref="CompileQuery{TResult}" /> intercepts the LINQ tree before
///     EF's translation pipeline and ships it to the server. Real capture logic lands in C2;
///     SaveChanges lands in Step 10.
/// </summary>
public class InfoCarrierDatabase : IDatabase
{
    private readonly IInfoCarrierClient _client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierDatabase" /> class.
    /// </summary>
    public InfoCarrierDatabase(IDbContextOptions options)
    {
        _client = options.Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .First()
            .InfoCarrierClient!;
    }

    /// <summary>
    ///     The client used to ship operations to the server.
    /// </summary>
    protected IInfoCarrierClient Client => _client;

    /// <inheritdoc />
    public virtual Func<QueryContext, TResult> CompileQuery<TResult>(Expression query, bool async)
        => throw new NotImplementedException("Client raw-capture CompileQuery lands in C2.");

    /// <inheritdoc />
    public virtual Expression<Func<QueryContext, TResult>> CompileQueryExpression<TResult>(Expression query, bool async)
        => throw new NotImplementedException("Precompiled queries are not supported by InfoCarrier.");

    /// <inheritdoc />
    public virtual int SaveChanges(IList<IUpdateEntry> entries)
        => throw new NotImplementedException("SaveChanges lands in Step 10.");

    /// <inheritdoc />
    public virtual Task<int> SaveChangesAsync(IList<IUpdateEntry> entries, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("SaveChanges lands in Step 10.");
}
