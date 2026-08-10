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

    private readonly InfoCarrierPayloadLimits _limits;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemTextJsonInfoCarrierSerializer" />
    ///     class with the default payload limits.
    /// </summary>
    public SystemTextJsonInfoCarrierSerializer()
        : this(InfoCarrierPayloadLimits.Default)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemTextJsonInfoCarrierSerializer" />
    ///     class.
    /// </summary>
    /// <param name="limits">
    ///     The size bounds applied to what this serializer deserializes (milestone M5).
    ///     Default-on for the request direction: the parameterless constructor uses
    ///     <see cref="InfoCarrierPayloadLimits.Default" />, and opting out means constructing an
    ///     <see cref="InfoCarrierPayloadLimits" /> with a null maximum. Which bound applies to a
    ///     given call is decided by whether the type is an <see cref="Common.IInfoCarrierRequest" />
    ///     — see that interface for why the two directions are not the same question.
    /// </param>
    public SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits limits)
        => _limits = limits;

    /// <summary>
    ///     The payload limits this serializer applies.
    /// </summary>
    public InfoCarrierPayloadLimits Limits => _limits;

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] payload)
    {
        // Before the parse, never after: the allocation the parse costs is what is being bounded.
        _limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        return JsonSerializer.Deserialize<T>(payload, Options);
    }

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
        _limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        using var stream = new MemoryStream(payload);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
    }
}
