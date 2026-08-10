// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.Common;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     The payload size bound (milestone M5, requirements §4.1): default-on in the request
///     direction, opt-out, refused before the parse with a message naming both the size and the
///     limit.
/// </summary>
public class PayloadLimitsTest
{
    private static byte[] Payload(int bytes)
    {
        // Valid JSON of a known length, so a refusal cannot be confused with a parse failure —
        // a string of `bytes - 2` characters, quoted.
        byte[] payload = new byte[bytes];
        Array.Fill(payload, (byte)'x');
        payload[0] = (byte)'"';
        payload[^1] = (byte)'"';
        return payload;
    }

    private static byte[] Envelope(SystemTextJsonInfoCarrierSerializer serializer, int payloadBytes)
        => serializer.Serialize(new InfoCarrierEnvelope
        {
            ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
            Operation = InfoCarrierOperation.Query,
            Payload = new byte[payloadBytes],
        });

    [Fact]
    public void The_request_bound_is_on_by_default_and_the_response_bound_is_not()
    {
        Assert.Equal(
            InfoCarrierPayloadLimits.DefaultMaxRequestBytes,
            InfoCarrierPayloadLimits.Default.MaxRequestBytes);
        Assert.Null(InfoCarrierPayloadLimits.Default.MaxResponseBytes);

        SystemTextJsonInfoCarrierSerializer serializer = new();
        Assert.Equal(
            InfoCarrierPayloadLimits.DefaultMaxRequestBytes,
            serializer.Limits.MaxRequestBytes);
    }

    /// <summary>
    ///     The direction is decided by the marker interface, and every wire type towards the
    ///     server carries it.
    /// </summary>
    [Fact]
    public void The_request_types_are_the_ones_marked()
    {
        Assert.True(InfoCarrierPayloadLimits.IsRequest<QueryDataRequest>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<SaveChangesRequest>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<SavepointRequest>());
        Assert.True(InfoCarrierPayloadLimits.IsRequest<InfoCarrierEnvelope>());

        Assert.False(InfoCarrierPayloadLimits.IsRequest<QueryDataResult>());
        Assert.False(InfoCarrierPayloadLimits.IsRequest<SaveChangesResult>());
        Assert.False(InfoCarrierPayloadLimits.IsRequest<TransactionResult>());
    }

    [Fact]
    public void An_oversized_request_is_refused_with_both_numbers()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(1024));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => serializer.Deserialize<InfoCarrierEnvelope>(Envelope(serializer, 8192)));

        Assert.Contains("1024 bytes", ex.Message);
        Assert.Contains(nameof(InfoCarrierPayloadLimits.MaxRequestBytes), ex.Message);
    }

    [Fact]
    public async Task The_async_path_is_bounded_too()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(1024));

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await serializer.DeserializeAsync<InfoCarrierEnvelope>(Envelope(serializer, 8192)));
    }

    /// <summary>
    ///     The case that made the direction split necessary: a result the caller asked for is not
    ///     bounded by the request limit. C37 measured four Northwind spec tests going red at
    ///     560 MB and 111 MB when it was.
    /// </summary>
    [Fact]
    public void A_response_is_not_bounded_by_the_request_limit()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(8));

        // `string` is not an IInfoCarrierRequest, so the request bound does not reach it.
        Assert.NotNull(serializer.Deserialize<string>(Payload(1024)));
    }

    /// <summary>
    ///     …but an application that wants a page-size policy can set one, and then it applies.
    /// </summary>
    [Fact]
    public void A_response_bound_applies_once_set()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(
            new InfoCarrierPayloadLimits(maxRequestBytes: null, maxResponseBytes: 64));

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => serializer.Deserialize<string>(Payload(1024)));
        Assert.Contains(nameof(InfoCarrierPayloadLimits.MaxResponseBytes), ex.Message);
    }

    /// <summary>
    ///     A payload at the limit passes: the bound is "exceeds", not "reaches".
    /// </summary>
    [Fact]
    public void A_payload_at_the_limit_is_accepted()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(
            new InfoCarrierPayloadLimits(maxRequestBytes: null, maxResponseBytes: 64));

        Assert.NotNull(serializer.Deserialize<string>(Payload(64)));
    }

    /// <summary>
    ///     Opting out is spelled as an explicit null, not as a very large number.
    /// </summary>
    [Fact]
    public void The_request_bound_can_be_opted_out_of()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(null));

        Assert.Null(serializer.Limits.MaxRequestBytes);
        Assert.NotNull(serializer.Deserialize<InfoCarrierEnvelope>(Envelope(serializer, 8192)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_limit_is_a_configuration_error(int max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InfoCarrierPayloadLimits(max));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InfoCarrierPayloadLimits(null, max));
    }

    /// <summary>
    ///     Serialization is not bounded — only what arrives from elsewhere is. A server that
    ///     produced a large result should say so through the transport, not fail to write it.
    /// </summary>
    [Fact]
    public void Serialization_is_not_bounded()
    {
        var serializer = new SystemTextJsonInfoCarrierSerializer(new InfoCarrierPayloadLimits(8));

        Assert.True(serializer.Serialize(new string('x', 1024)).Length > 8);
    }

    /// <summary>
    ///     A payload under the limit still has to be valid, so a refusal cannot be mistaken for a
    ///     limit that silently swallows malformed input.
    /// </summary>
    [Fact]
    public void An_undersized_but_malformed_payload_still_fails_as_json()
        => Assert.Throws<JsonException>(
            () => new SystemTextJsonInfoCarrierSerializer().Deserialize<string>("{not json"u8.ToArray()));
}
