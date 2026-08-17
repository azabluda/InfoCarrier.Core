// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace Northwind.Client.Wire;

/// <summary>
///     Answers, from inside the browser, whether a response really is handed over as a live stream
///     (<c>docs/architecture.md</c> §6a <b>D7</b>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Why this exists.</b> Blazor WebAssembly is documented to buffer a whole response
///         unless asked not to, and <see cref="InfoCarrier.Core.HttpInfoCarrierTransport" /> asks
///         through an <see cref="HttpRequestOptionsKey{TValue}" />. <b>A wrong or stale key would
///         be silent</b> — <c>HttpRequestOptions</c> is a dictionary and accepts anything — so the
///         app would keep working, keep looking correct, and quietly buffer every result. That is
///         the shape of the two WebAssembly defaults this sample has already been caught by.
///     </para>
///     <para>
///         <b>The discriminator is what the response stream wraps, and it took three attempts to
///         find one that could see anything.</b> The outer type is
///         <c>StreamContent+ReadOnlyStream</c> whichever way the request went, and that wrapper
///         reports the same <see cref="System.IO.Stream.CanSeek" /> either way — so both of the
///         obvious checks returned identical answers for the two requests and read as "the option
///         made no difference" when they showed nothing at all. What differs is <em>inside</em>:
///         <c>BrowserHttpReadStream</c> for a live response, a <see cref="System.IO.MemoryStream" />
///         for one that was downloaded first.
///     </para>
///     <para>
///         <b>Three requests, not two, and the third is there to falsify the other two.</b> A probe
///         that cannot produce a different answer for a different input is not measuring anything —
///         the same discipline <c>StreamingOverHttpTest</c> needed. So one request also asks for
///         <see cref="HttpCompletionOption.ResponseContentRead" />, which must buffer; if all three
///         come back identical, this reports itself blind rather than reporting a verdict.
///     </para>
///     <para>
///         It probes the app's own <c>index.html</c> rather than the InfoCarrier endpoint on
///         purpose: the question is about the <em>browser's HTTP stack</em> and the option key, and
///         mixing a query into it would only add ways for the answer to be about something else.
///     </para>
/// </remarks>
public sealed class BrowserStreamingProbe(HttpClient httpClient)
{
    /// <summary>
    ///     The same key <see cref="InfoCarrier.Core.HttpInfoCarrierTransport" /> sets, repeated here
    ///     deliberately: this probe answers for the key, so it has to name it itself.
    /// </summary>
    private static readonly HttpRequestOptionsKey<bool> BrowserResponseStreaming
        = new("WebAssemblyEnableStreamingResponse");

    private const string LiveStream = "BrowserHttpReadStream";

    private readonly HttpClient _httpClient = httpClient;

    /// <summary>
    ///     Fetches the same file three ways and reports what each response stream turned out to be.
    /// </summary>
    public async Task<string> DescribeAsync(CancellationToken cancellationToken = default)
    {
        string withOption = await ProbeAsync(
            HttpCompletionOption.ResponseHeadersRead, enableStreaming: true, cancellationToken).ConfigureAwait(false);
        string withoutOption = await ProbeAsync(
            HttpCompletionOption.ResponseHeadersRead, enableStreaming: false, cancellationToken).ConfigureAwait(false);
        string contentRead = await ProbeAsync(
            HttpCompletionOption.ResponseContentRead, enableStreaming: false, cancellationToken).ConfigureAwait(false);

        string verdict =
            withOption == withoutOption && withoutOption == contentRead
                ? "BLIND - all three requests produced the same stream, so this cannot see the difference at all"
                : withOption.Contains(LiveStream, StringComparison.Ordinal)
                    ? withoutOption.Contains(LiveStream, StringComparison.Ordinal)
                        ? "STREAMING, BY DEFAULT - a live read stream arrives with or without the option; "
                            + "ResponseHeadersRead is what decides it in this runtime"
                        : "STREAMING, OPTION-GATED - the option is what turns the live read stream on"
                    : "NOT STREAMING - even with the option the response is not a live read stream";

        return $"{verdict}\n"
            + $"headers-read + option:   {withOption}\n"
            + $"headers-read, no option: {withoutOption}\n"
            + $"content-read, no option: {contentRead}";
    }

    private async Task<string> ProbeAsync(
        HttpCompletionOption completionOption, bool enableStreaming, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "index.html");

        if (enableStreaming)
        {
            request.Options.Set(BrowserResponseStreaming, true);
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(request, completionOption, cancellationToken)
            .ConfigureAwait(false);

        Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Describe(stream);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     The stream, and whatever it is a wrapper around.
    /// </summary>
    /// <remarks>
    ///     Reflection over a private BCL field is fragile by construction, and that is acceptable
    ///     here and nowhere else: this is a diagnostic in a sample, it names what it could not find
    ///     rather than throwing, and the alternative is a check that cannot answer its own question.
    /// </remarks>
    private static string Describe(Stream stream)
    {
        string outer = stream.GetType().Name;

        for (Type? type = stream.GetType(); type is not null; type = type.BaseType)
        {
            System.Reflection.FieldInfo? field = type.GetField(
                "_innerStream",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (field?.GetValue(stream) is Stream inner)
            {
                return $"{outer} -> {inner.GetType().Name}";
            }
        }

        return $"{outer} (no inner stream)";
    }
}
