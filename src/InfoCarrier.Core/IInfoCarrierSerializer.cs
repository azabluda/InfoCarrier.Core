// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core;

/// <summary>
///     Abstraction over the serialization format used on the wire (requirements §4.1).
///     The core library never hardcodes JSON/protobuf/MessagePack; envelopes and payloads
///     serialize through this seam. The default implementation is System.Text.Json with
///     source-generated contexts (requirements §4.5).
/// </summary>
public interface IInfoCarrierSerializer
{
    /// <summary>
    ///     The size bounds this serializer applies (milestone M5).
    /// </summary>
    /// <remarks>
    ///     On the interface rather than on the implementation because a <em>streamed</em> response
    ///     is bounded by whoever reads the stream, not by whoever deserializes a payload —
    ///     <see cref="HttpInfoCarrierTransport" /> has to see
    ///     <see cref="InfoCarrierPayloadLimits.MaxResponseBytes" /> in order to count against it,
    ///     and before this it could only have got there by testing for a concrete serializer type.
    ///     An implementation that bounds nothing returns
    ///     <c>new InfoCarrierPayloadLimits(null, null)</c>.
    /// </remarks>
    InfoCarrierPayloadLimits Limits { get; }

    /// <summary>
    ///     Serializes a value to a byte payload.
    /// </summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The serialized bytes.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>
    ///     Deserializes a value from a byte payload.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="payload">The serialized bytes.</param>
    /// <returns>The deserialized value.</returns>
    T? Deserialize<T>(byte[] payload);

    /// <summary>
    ///     Serializes a value to a byte payload asynchronously.
    /// </summary>
    ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deserializes a value from a byte payload asynchronously.
    /// </summary>
    ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default);
}
