// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core;

/// <summary>
///     InfoCarrier provider logging definitions. Providers supply their own subclass so EF's
///     <c>DiagnosticsLogger</c> resolves provider-specific event payloads.
/// </summary>
public class InfoCarrierLoggingDefinitions : LoggingDefinitions
{
}
