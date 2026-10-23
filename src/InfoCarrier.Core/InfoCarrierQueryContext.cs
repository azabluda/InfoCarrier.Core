// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;

namespace InfoCarrier.Core;

/// <summary>
///     The client-side <see cref="QueryContext" />. It carries the injected logging replay
///     dependency beside the core dependencies.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="InfoCarrierQueryContext" /> class.
/// </remarks>
public class InfoCarrierQueryContext(QueryContextDependencies dependencies, ILoggerFactory loggerFactory)
    : QueryContext(dependencies)
{
    internal ServerLogReplay ServerLogReplay { get; } = new(loggerFactory);
}
