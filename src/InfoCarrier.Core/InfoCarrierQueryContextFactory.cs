// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace InfoCarrier.Core;

/// <summary>
///     Creates the client-side <see cref="QueryContext" />. InfoCarrier remotes queries, so the
///     query context carries no provider-specific state beyond the core dependencies.
/// </summary>
public class InfoCarrierQueryContextFactory : IQueryContextFactory
{
    private readonly QueryContextDependencies _dependencies;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierQueryContextFactory" /> class.
    /// </summary>
    public InfoCarrierQueryContextFactory(QueryContextDependencies dependencies)
        => _dependencies = dependencies;

    /// <inheritdoc />
    public QueryContext Create()
        => new InfoCarrierQueryContext(_dependencies);
}
