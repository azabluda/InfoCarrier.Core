// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;

namespace InfoCarrier.Core;

/// <summary>
///     Default <see cref="IInfoCarrierSerializer" /> backed by System.Text.Json
///     (requirements §4.1, §4.5), reflection-free through <see cref="InfoCarrierJsonContext" />.
///     <para>
///         This layer sees only flat records: an entity graph reaches it already reduced to a
///         <c>byte[]</c> by <see cref="Expressions.ExpressionJsonContext" />, which carries the
///         node model's own repeat handling. It therefore enables no reference handling of its
///         own — see <see cref="InfoCarrierJsonContext" /> for why it cannot, and why that costs
///         nothing.
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
    private static readonly JsonSerializerOptions Options = InfoCarrierJsonContext.Default.Options;

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
