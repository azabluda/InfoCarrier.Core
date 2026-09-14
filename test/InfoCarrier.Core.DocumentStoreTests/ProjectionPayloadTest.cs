// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     What actually crosses the wire when a projection contains something the store cannot
///     translate.
/// </summary>
/// <remarks>
///     <para>
///         <b>WRITTEN TO CHECK A CLAIM THIS REPOSITORY WAS ABOUT TO MAKE ABOUT ITSELF
///         (2026-09-14).</b> Tier D's wire-free control fails
///         <c>Select_untranslatable_method_on_associate_scalar_property</c> and Tier D passes it,
///         and that was recorded in <c>test/tier-d-control-allowances.txt</c> as a capability gain:
///         the projection split removes the untranslatable node, so the store never sees what it
///         cannot translate.
///     </para>
///     <para>
///         <b>The green test cannot tell that story from a much worse one.</b> Shipping
///         <c>Set&lt;Customer&gt;()</c> whole and doing the projection on the client gives the
///         IDENTICAL answer, with every document crossing the wire — a violation of requirements
///         §3.3 and wire-protocol W1 wearing a passing test. ADR-010 says the split rewrites a
///         projection into a tuple carrier precisely so that does not happen, but a design document
///         and the code it describes can both be right and still not say what happens.
///     </para>
///     <para>
///         <b>So this measures the bytes.</b> The projection reads ONE owned scalar,
///         <c>Address.Postcode</c>, and the seed gives each customer a distinct city the query never
///         asks for. If a city name appears in the response, the whole document crossed.
///     </para>
/// </remarks>
public class ProjectionPayloadTest(DocumentStoreFixture fixture) : IClassFixture<DocumentStoreFixture>
{
    /// <summary>A method no store can translate, and the client must evaluate.</summary>
    private static string Scramble(string postcode) => "#" + postcode;

    [Fact]
    public async Task An_untranslatable_projection_does_not_drag_the_whole_document_over()
    {
        var requests = new List<string>();
        var responses = new List<string>();

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var envelopeServer = new InfoCarrierEnvelopeServer(
            new InProcessInfoCarrierServer(fixture.ServerProvider), serializer);

        DbContextOptions<ShopClientContext> options = new DbContextOptionsBuilder<ShopClientContext>()
            .UseInfoCarrier(
                new TransportInfoCarrierClient(
                    new RecordingTransport(envelopeServer, requests, responses), serializer))
            .Options;

        await using var context = new ShopClientContext(options);

        List<string> scrambled = await context.Customers
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => Scramble(c.Address.Postcode))
            .ToListAsync();

        // The answer is right, which is what the specification test also checks.
        Assert.Equal(["#10115", "#1100", "#20095"], scrambled);

        string response = Readable(responses);

        // THE POINT. The query asked for postcodes; cities are the part of the same owned document
        // it did not ask for. Their absence is what says the projection was pushed down rather than
        // the document pulled over.
        Assert.DoesNotContain("Berlin", response);
        Assert.DoesNotContain("Lisbon", response);
        Assert.DoesNotContain("Hamburg", response);

        // And the postcodes DID come back, so the absence above is not an empty response.
        Assert.Contains("10115", response);

        // The store never saw the method it cannot translate: the split cut it before the wire.
        Assert.DoesNotContain(nameof(Scramble), Readable(requests));
    }

    /// <summary>The envelope's JSON, with every base64 blob inside it decoded.</summary>
    /// <remarks>
    ///     <b>THE FIRST VERSION OF THIS TEST SEARCHED THE RAW JSON AND WOULD HAVE PASSED
    ///     VACUOUSLY.</b> The rows ride as base64 in <c>serializedResults</c>, so
    ///     <c>Assert.DoesNotContain("Berlin", …)</c> was true of every possible payload — including
    ///     one carrying every document in the store, which is the exact failure this test exists to
    ///     detect. <b>A negative assertion is worth only as much as the proof that what it looks
    ///     for COULD have appeared</b>, which is why the postcode is asserted positively in the
    ///     same decoded text.
    /// </remarks>
    private static string Readable(IEnumerable<string> envelopes)
    {
        var text = new StringBuilder();

        foreach (string envelope in envelopes)
        {
            text.AppendLine(envelope);

            foreach (Match match in Base64Blob.Matches(envelope))
            {
                try
                {
                    text.AppendLine(Encoding.UTF8.GetString(Convert.FromBase64String(match.Value)));
                }
                catch (FormatException)
                {
                    // Not base64 after all. The raw line above already carries it.
                }
            }
        }

        return text.ToString();
    }

    private static readonly Regex Base64Blob = new("[A-Za-z0-9+/]{24,}={0,2}", RegexOptions.Compiled);

    private sealed class RecordingTransport(
        InfoCarrierEnvelopeServer server,
        List<string> requests,
        List<string> responses)
        : IInfoCarrierTransport
    {
        public async Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            requests.Add(Encoding.UTF8.GetString(request.Payload));
            InfoCarrierEnvelope response = await server.DispatchAsync(request, cancellationToken);
            responses.Add(Encoding.UTF8.GetString(response.Payload));
            return response;
        }
    }
}
