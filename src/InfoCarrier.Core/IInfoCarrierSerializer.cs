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
