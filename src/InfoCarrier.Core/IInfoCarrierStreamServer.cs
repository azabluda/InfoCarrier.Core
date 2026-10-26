// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;

namespace InfoCarrier.Core;

/// <summary>Optional streaming capability for servers and server decorators.</summary>
public interface IInfoCarrierStreamServer
{
    /// <summary>Retains operation resources until enumeration completes or is disposed.</summary>
    IAsyncEnumerable<QueryStreamFrame> QueryFramesAsync(QueryDataRequest request,
        CancellationToken cancellationToken = default);
}
