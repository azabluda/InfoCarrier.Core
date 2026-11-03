// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Query;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.Json;

namespace InfoCarrier.Core.Wire;

/// <summary>Bounds transport input and dispatches to an explicitly registered protocol codec.</summary>
public sealed class InfoCarrierWireProtocol
{
    private readonly FrozenDictionary<int, IWireProtocolCodec> _codecs;
    private readonly IWireProtocolCodec _current;
    private readonly IWireProtocolCodec[] _metadataCodecs;
    private readonly ConcurrentDictionary<Type, JsonSerializerOptions> _options = new();

    /// <summary>Creates protocol selection from the host's codec services.</summary>
    /// <remarks>Codecs are singleton services. Duplicate versions and a missing current codec are configuration errors.</remarks>
    public InfoCarrierWireProtocol(IEnumerable<IWireProtocolCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        IWireProtocolCodec[] selected = codecs.ToArray();
        if (selected.GroupBy(codec => codec.Version).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Register exactly one wire protocol codec for each version.");
        }
        _codecs = selected.ToFrozenDictionary(codec => codec.Version);
        _current = _codecs.GetValueOrDefault(InfoCarrierEnvelope.CurrentProtocolVersion)
            ?? throw new InvalidOperationException("A codec for the current wire protocol version must be registered.");
        _metadataCodecs = [_current, .. selected.Where(codec => !ReferenceEquals(codec, _current))];
    }

    internal JsonSerializerOptions OptionsFor(Type type)
        => _options.GetOrAdd(type, static (key, protocols) => protocols.FindOptions(key), this);

    private JsonSerializerOptions FindOptions(Type type)
    {
        foreach (IWireProtocolCodec codec in _metadataCodecs)
        {
            // Generated metadata answers membership without assembly scanning or reflection fallback.
            if (codec.JsonContext.GetTypeInfo(type) is not null) { return codec.JsonContext.Options; }
        }
        return _current.JsonContext.Options;
    }

    private IWireProtocolCodec Get(int version) => _codecs.TryGetValue(version, out IWireProtocolCodec? codec)
        ? codec : throw new InfoCarrierProtocolException(version, InfoCarrierEnvelope.CurrentProtocolVersion);

    /// <summary>Gets whether the server implements the identified contract.</summary>
    public bool IsSupported(int version) => _codecs.ContainsKey(version);

    /// <summary>Reads identification before selecting the complete envelope representation.</summary>
    public async ValueTask<InfoCarrierEnvelope?> ReadEnvelopeAsync(IInfoCarrierSerializer serializer,
        Stream source, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = await BufferRequestAsync<InfoCarrierEnvelope>(serializer, source, cancellationToken).ConfigureAwait(false);
        int version = (await serializer.DeserializeAsync<QueryRequestIdentification>(buffer, cancellationToken).ConfigureAwait(false))?.ProtocolVersion ?? 0;
        buffer.Position = 0;
        // An unknown envelope retains operation identity for diagnostics. Dispatch refuses its
        // version before opening opaque operation bytes. Streamed requests must be admitted first.
        IWireProtocolCodec codec = _codecs.TryGetValue(version, out IWireProtocolCodec? supported)
            ? supported : _current;
        return await codec.ReadEnvelopeAsync(serializer, buffer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks the operation vocabulary of the identified contract.</summary>
    public bool IsKnownOperation(int version, InfoCarrierOperation operation)
        => _codecs.TryGetValue(version, out IWireProtocolCodec? codec) && codec.IsKnownOperation(operation);

    /// <summary>Decodes an operation using its already admitted contract.</summary>
    public T? Deserialize<T>(IInfoCarrierSerializer serializer, byte[] payload, int version)
        => Get(version).Deserialize<T>(serializer, payload, Limits(serializer));

    /// <summary>Encodes a response using the requesting peer's contract.</summary>
    public byte[] Serialize<T>(IInfoCarrierSerializer serializer, T value, int version)
        => Get(version).Serialize(serializer, value);

    /// <summary>Decodes a streamed query after its version header has been checked.</summary>
    public async ValueTask<QueryDataRequest?> ReadQueryAsync(IInfoCarrierSerializer serializer,
        Stream source, int version, CancellationToken cancellationToken)
    {
        IWireProtocolCodec codec = Get(version);
        // Bound bytes and establish body identity before constructing a version-specific graph.
        using MemoryStream buffer = await BufferRequestAsync<QueryDataRequest>(serializer, source, cancellationToken).ConfigureAwait(false);
        QueryRequestIdentification? identification = await serializer.DeserializeAsync<QueryRequestIdentification>(buffer, cancellationToken).ConfigureAwait(false);
        int bodyVersion = identification?.ProtocolVersion ?? 0;
        if (bodyVersion != version) { throw new InfoCarrierProtocolException(bodyVersion, InfoCarrierEnvelope.CurrentProtocolVersion); }
        buffer.Position = 0;
        QueryDataRequest? request = await codec.ReadQueryAsync(serializer, buffer, Limits(serializer), cancellationToken).ConfigureAwait(false);
        if (request is not null && request.ProtocolVersion != version)
        {
            throw new InfoCarrierProtocolException(request.ProtocolVersion, InfoCarrierEnvelope.CurrentProtocolVersion);
        }
        return request;
    }

    /// <summary>Writes frames using the selected contract's codec.</summary>
    public ValueTask WriteFramesAsync(Stream destination, IAsyncEnumerable<QueryStreamFrame> frames,
        int version, CancellationToken cancellationToken)
        => Get(version).WriteFramesAsync(destination, frames, cancellationToken);

    private static async ValueTask<MemoryStream> BufferRequestAsync<T>(IInfoCarrierSerializer serializer, Stream source, CancellationToken token)
    {
        using Stream? bounded = Limits(serializer).CreateReadLimit<T>(source);
        var buffer = new MemoryStream();
        try
        {
            await (bounded ?? source).CopyToAsync(buffer, token).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        catch { buffer.Dispose(); throw; }
    }
    private static InfoCarrierPayloadLimits Limits(IInfoCarrierSerializer serializer) => serializer switch
    {
        InfoCarrierSerializerBoundary boundary => boundary.Limits,
        SystemTextJsonInfoCarrierSerializer json => json.Limits,
        _ => InfoCarrierPayloadLimits.Default,
    };
}
