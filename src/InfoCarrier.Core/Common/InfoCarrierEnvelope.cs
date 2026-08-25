// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     The versioned envelope every request and response carries (wire-protocol §1).
///     Protocol version is present from day 1; the payload is the operation-specific body
///     serialized via the configured <see cref="IInfoCarrierSerializer" />.
/// </summary>
/// <remarks>
///     Marked <see cref="IInfoCarrierRequest" /> although it carries traffic in both directions:
///     the type cannot say which leg it is on, and the leg that matters is the one where an
///     untrusted caller chooses the bytes. A client that would rather not bound the responses it
///     buffers can raise <see cref="InfoCarrierPayloadLimits.MaxRequestBytes" /> on its own
///     serializer. The type does not distinguish the two legs.
/// </remarks>
public sealed record InfoCarrierEnvelope : IInfoCarrierRequest
{
    /// <summary>
    ///     The current wire contract major version.
    /// </summary>
    public const int CurrentProtocolVersion = 1;

    /// <summary>
    ///     The wire contract version. A server rejects an unsupported major version with a
    ///     well-known error payload (wire-protocol §1 versioning policy).
    /// </summary>
    public required int ProtocolVersion { get; init; } = CurrentProtocolVersion;

    /// <summary>
    ///     The operation discriminator.
    /// </summary>
    public required InfoCarrierOperation Operation { get; init; }

    /// <summary>
    ///     The operation-specific payload, serialized via the configured serializer.
    ///     For <see cref="InfoCarrierOperation.Query" /> this is a <see cref="QueryDataRequest" />;
    ///     for <see cref="InfoCarrierOperation.SaveChanges" /> a <see cref="SaveChangesRequest" />.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    ///     An optional caller-supplied id. The server copies it onto the response to the request
    ///     it answers, so a caller that has more than one request in flight over one transport can
    ///     match an answer to the request that produced it. Nothing in this package sets it, and
    ///     it is null when a caller does not.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    ///     The server-side failure this response reports, or <see langword="null" /> on success
    ///     (wire-protocol W5).
    /// </summary>
    /// <remarks>
    ///     A response envelope carries either a <see cref="Payload" /> or a
    ///     <see cref="Fault" />, never both meaningfully — the payload of a fault response is a
    ///     placeholder, because the field is required and a zero-length body is not valid JSON.
    ///     Checking this before reading the payload is what makes an error a *result* of the
    ///     protocol rather than an accident of running in one process.
    /// </remarks>
    public InfoCarrierFault? Fault { get; init; }
}
