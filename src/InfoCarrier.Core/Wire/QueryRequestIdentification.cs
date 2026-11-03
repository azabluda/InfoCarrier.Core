// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Wire;

/// <summary>A read-only transport projection used before selecting a query or envelope decoder.</summary>
public sealed record QueryRequestIdentification : Common.IInfoCarrierRequest
{
    /// <summary>Gets the body identification; zero means it was omitted.</summary>
    public int ProtocolVersion { get; init; }
}
