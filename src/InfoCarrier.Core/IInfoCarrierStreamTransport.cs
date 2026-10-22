// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     Exchanges request and response streams without buffering either body.
/// </summary>
public interface IInfoCarrierStreamTransport
{
    /// <summary>
    ///     Sends one operation and returns ownership of its response stream.
    /// </summary>
    ValueTask<IInfoCarrierResponse> SendAsync(
        InfoCarrierOperation operation,
        Func<Stream, CancellationToken, ValueTask> writeRequest,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Owns a transport response and its readable content stream.
/// </summary>
public interface IInfoCarrierResponse : IAsyncDisposable
{
    /// <summary>
    ///     Gets the response content stream.
    /// </summary>
    Stream Content { get; }
}
