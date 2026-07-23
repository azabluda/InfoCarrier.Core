// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.Core;

/// <summary>
///     Default <see cref="IInfoCarrierSerializer" /> backed by System.Text.Json
///     (requirements §4.1, §4.5). Reference handling is enabled so entity graphs with
///     circular navigation references round-trip (wire-protocol §3).
/// </summary>
public sealed class SystemTextJsonInfoCarrierSerializer : IInfoCarrierSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] payload)
        => JsonSerializer.Deserialize<T>(payload, Options);

    /// <inheritdoc />
    public async ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(payload);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
    }
}
