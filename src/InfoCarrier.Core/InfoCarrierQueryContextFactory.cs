// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     Creates the client-side <see cref="QueryContext" />. InfoCarrier remotes queries, so the
///     query context carries no provider-specific state beyond the core dependencies.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierQueryContextFactory" /> class.
/// </remarks>
public class InfoCarrierQueryContextFactory(
    QueryContextDependencies dependencies,
    ILoggerFactory loggerFactory) : IQueryContextFactory
{
    private readonly QueryContextDependencies _dependencies = dependencies;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <inheritdoc />
    public virtual QueryContext Create()
        => new InfoCarrierQueryContext(_dependencies, _loggerFactory);
}
