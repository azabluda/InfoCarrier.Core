// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.Core;

/// <summary>
///     Default <see cref="IInfoCarrierSerializer" /> backed by System.Text.Json
///     (requirements §4.1, §4.5), reflection-free through <see cref="InfoCarrierJsonContext" />.
///     <para>
///         Live queries and buffered results contain typed graphs. Historical payloads select
///         their independent generated metadata. Graph references use explicit node identifiers;
///         neither representation enables serializer-level reference handling.
///     </para>
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="SystemTextJsonInfoCarrierSerializer" />
///     class.
/// </remarks>
/// <param name="limits">
///     The size bounds applied to what this serializer deserializes (milestone M5).
///     Default-on for the request direction: the parameterless constructor uses
///     <see cref="InfoCarrierPayloadLimits.Default" />, and opting out means constructing an
///     <see cref="InfoCarrierPayloadLimits" /> with a null maximum. Which bound applies to a
///     given call is decided by whether the type is an <see cref="Common.IInfoCarrierRequest" />
///     — see that interface for why the two directions are not the same question.
/// </param>
/// <remarks>Constructors without protocol services use only the live serialization context.</remarks>
public sealed class SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits limits) : IInfoCarrierSerializer
{
    // The source-generated context's own options, not a hand-built instance, and the three
    // settings that used to be written here now live on it as [JsonSourceGenerationOptions].
    //
    // A context carries its own options -- ExpressionJsonContext states the same thing twice, for
    // MaxDepth and for NumberHandling, both of which did nothing when they were tried on this
    // class first. Building options here and merely *pointing* TypeInfoResolver at the context
    // would work, but it would put the wire format in two places that have to agree; taking the
    // context's options makes the context the single statement of it.
    //
    // Why a resolver at all: without one, System.Text.Json serializes reflectively. That is fine
    // on a server and fails outright in a trimmed WebAssembly build, where the SDK sets
    // JsonSerializerIsReflectionEnabledByDefault=false (M8's Phase 2). The expression tree was
    // already covered by ExpressionJsonContext; this closes the envelope half.
    private readonly Wire.InfoCarrierWireProtocol? _protocols;
    private JsonSerializerOptions OptionsFor<T>() => _protocols?.OptionsFor(typeof(T)) ?? InfoCarrierJsonContext.Default.Options;

    /// <summary>Creates a serializer using the host's protocol codecs and the selected payload limits.</summary>
    public SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits limits, Wire.InfoCarrierWireProtocol protocols) : this(limits)
    {
        ArgumentNullException.ThrowIfNull(protocols);
        _protocols = protocols;
    }

    private readonly InfoCarrierPayloadLimits _limits = limits;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SystemTextJsonInfoCarrierSerializer" />
    ///     class with the default payload limits.
    /// </summary>
    public SystemTextJsonInfoCarrierSerializer()
        : this(InfoCarrierPayloadLimits.Default)
    {
    }

    /// <summary>
    ///     The payload limits this serializer applies.
    /// </summary>
    public InfoCarrierPayloadLimits Limits => _limits;

    /// <inheritdoc />
    public ValueTask SerializeAsync<T>(
        Stream destination,
        T value,
        CancellationToken cancellationToken = default)
        => new(JsonSerializer.SerializeAsync(destination, value, OptionsFor<T>(), cancellationToken));

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        using Stream? bounded = _limits.CreateReadLimit<T>(source);
        return await JsonSerializer.DeserializeAsync<T>(bounded ?? source, OptionsFor<T>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, OptionsFor<T>());

    /// <inheritdoc />
    public T? Deserialize<T>(byte[] payload)
    {
        // Before the parse, never after: the allocation the parse costs is what is being bounded.
        _limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        return JsonSerializer.Deserialize<T>(payload, OptionsFor<T>());
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> SerializeAsync<T>(T value, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, OptionsFor<T>(), cancellationToken).ConfigureAwait(false);
        return stream.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<T?> DeserializeAsync<T>(byte[] payload, CancellationToken cancellationToken = default)
    {
        _limits.Guard<T>(payload.Length, $"payload for '{typeof(T).Name}'");
        using var stream = new MemoryStream(payload);
        return await JsonSerializer.DeserializeAsync<T>(stream, OptionsFor<T>(), cancellationToken).ConfigureAwait(false);
    }
}
