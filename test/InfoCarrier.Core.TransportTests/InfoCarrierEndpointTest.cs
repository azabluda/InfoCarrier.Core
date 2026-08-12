// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Northwind.Client.Transport;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class InfoCarrierEndpointTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    private static readonly IInfoCarrierSerializer Serializer = new SystemTextJsonInfoCarrierSerializer();

    [Fact]
    public async Task A_begin_transaction_envelope_comes_back_with_a_transaction_id()
    {
        var transport = new HttpInfoCarrierTransport(factory.CreateClient(), Serializer);

        InfoCarrierEnvelope response = await transport.SendAsync(
            new InfoCarrierEnvelope
            {
                ProtocolVersion = InfoCarrierEnvelope.CurrentProtocolVersion,
                Operation = InfoCarrierOperation.BeginTransaction,
                Payload = Serializer.Serialize<object?>(null),
            });

        Assert.Null(response.Fault);

        TransactionResult? result = Serializer.Deserialize<TransactionResult>(response.Payload);
        Assert.False(string.IsNullOrEmpty(result?.TransactionId));
    }

    [Fact]
    public async Task An_unsupported_protocol_version_is_refused_by_number()
    {
        var transport = new HttpInfoCarrierTransport(factory.CreateClient(), Serializer);

        InfoCarrierTransportException exception = await Assert.ThrowsAsync<InfoCarrierTransportException>(
            () => transport.SendAsync(
                new InfoCarrierEnvelope
                {
                    ProtocolVersion = 999,
                    Operation = InfoCarrierOperation.BeginTransaction,
                    Payload = Serializer.Serialize<object?>(null),
                }));

        Assert.Contains("999", exception.Message);
    }
}
