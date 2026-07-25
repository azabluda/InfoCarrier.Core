// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core;

/// <summary>
///     The client-side <see cref="QueryContext" />. InfoCarrier remotes queries, so this carries
///     no provider-specific state beyond the core dependencies.
/// </summary>
public class InfoCarrierQueryContext : QueryContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierQueryContext" /> class.
    /// </summary>
    public InfoCarrierQueryContext(QueryContextDependencies dependencies)
        : base(dependencies)
    {
    }
}
