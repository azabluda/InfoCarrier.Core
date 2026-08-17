// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     Creates the client-side <see cref="QueryContext" />. InfoCarrier remotes queries, so the
///     query context carries no provider-specific state beyond the core dependencies.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierQueryContextFactory" /> class.
/// </remarks>
public class InfoCarrierQueryContextFactory(QueryContextDependencies dependencies) : IQueryContextFactory
{
    private readonly QueryContextDependencies _dependencies = dependencies;

    /// <inheritdoc />
    public virtual QueryContext Create()
        => new InfoCarrierQueryContext(_dependencies);
}
