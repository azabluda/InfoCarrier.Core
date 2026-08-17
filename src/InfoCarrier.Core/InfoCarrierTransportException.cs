// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     A failure of the transport itself, as opposed to a failure the server reported.
/// </summary>
/// <remarks>
///     The distinction matters and is why this type exists. A server-side failure travels as
///     data in <c>InfoCarrierEnvelope.Fault</c> and is raised again on the client with its
///     original type (W5). This is the other case: the request never reached a server, or what
///     came back was not an envelope. Reporting that as an EF exception would be a lie about
///     where the fault is.
/// </remarks>
public sealed class InfoCarrierTransportException : Exception
{
    public InfoCarrierTransportException(string message)
        : base(message)
    {
    }

    public InfoCarrierTransportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
