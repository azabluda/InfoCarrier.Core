// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;

namespace Northwind.Client.Wire;

public static class BrowserStreamingProbe
{
    public static async Task<Result> Run(
        HttpClient client,
        HttpCompletionOption completion,
        bool enableStreaming,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "streaming-research-data");
        request.Options.Set(new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse"), enableStreaming);

        long started = Stopwatch.GetTimestamp();
        using HttpResponseMessage response = await client.SendAsync(request, completion, cancellationToken);
        TimeSpan headers = Stopwatch.GetElapsedTime(started);
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var oneByte = new byte[1];
        int bytesRead = await stream.ReadAsync(oneByte, cancellationToken);
        TimeSpan firstByte = Stopwatch.GetElapsedTime(started);

        return new Result(
            completion.ToString(),
            enableStreaming,
            stream.GetType().FullName ?? stream.GetType().Name,
            headers.TotalMilliseconds,
            firstByte.TotalMilliseconds,
            bytesRead == 1 ? (char)oneByte[0] : null);
    }

    public sealed record Result(
        string Completion,
        bool EnableStreaming,
        string StreamType,
        double HeadersMilliseconds,
        double FirstByteMilliseconds,
        char? FirstByte);
}
