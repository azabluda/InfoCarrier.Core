// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     The measurement that proves the wire streams (<c>docs/architecture.md</c> §6a <b>D7</b>).
/// </summary>
/// <remarks>
///     <para>
///         <b>A green suite cannot tell streaming from buffering</b>, which is the whole hazard D7
///         records: an implementation that quietly still buffers answers every existing test
///         identically. So this asserts the one thing buffering cannot do — <b>the client holds
///         part of the response while the server has not finished producing it</b> — and it
///         asserts it by construction rather than by timing.
///     </para>
///     <para>
///         <b>The instrument must not be the thing that buffers.</b>
///         <see cref="RecordingHandler" /> calls <c>ReadAsByteArrayAsync</c> on every response, so
///         every test that uses it reads a fully buffered body no matter what the wire did — those
///         tests prove correctness and can say nothing about streaming.
///     </para>
///     <para>
///         <b>And it must watch the right boundary, which cost one wrong version to learn.</b> The
///         first attempt timed a <see cref="DelegatingHandler" />, and <b>it passed against a
///         deliberately re-buffered wire</b> — because <c>HttpCompletionOption</c> is applied by
///         <see cref="HttpClient" /> <em>after</em> the handler pipeline returns, so a handler sees
///         the response headers at the same moment whichever option is in force. The boundary that
///         actually distinguishes the two is <see cref="IInfoCarrierTransport.SendQueryAsync" />
///         returning: under <c>ResponseHeadersRead</c> it returns on the header item, and under
///         <c>ResponseContentRead</c> it cannot return until the last row has arrived.
///     </para>
///     <para>
///         <b>This test has been falsified</b>: with that one option changed back, it fails on the
///         deadline below. A streaming test that passes against a buffering implementation is worth
///         less than no test, because it also certifies the regression it was written to catch.
///     </para>
/// </remarks>
public class StreamingOverHttpTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    /// <summary>
    ///     How long to wait for the response header before concluding the response was buffered.
    /// </summary>
    /// <remarks>
    ///     <b>This is a failure deadline, not a timing assertion.</b> The correct implementation
    ///     reaches the header in milliseconds and never approaches it; a buffering one cannot reach
    ///     it at all, because the gate below is what would have to open first. Ten seconds is
    ///     therefore chosen to be far past any plausible scheduling delay, so that hitting it means
    ///     "buffered" rather than "busy machine".
    /// </remarks>
    private static readonly TimeSpan HeaderDeadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task The_client_holds_the_response_header_before_the_server_has_produced_a_row()
    {
        var gate = new RowGate();

        using WebApplicationFactory<Program> gated = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInfoCarrierServer>();
                services.AddSingleton(gate);
                services.AddSingleton<IInfoCarrierServer>(
                    sp => new GatedServer(new InProcessInfoCarrierServer(sp), gate));
            }));

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        HttpClient httpClient = gated.CreateDefaultClient();

        var headers = new HeaderTimingTransport(new HttpInfoCarrierTransport(httpClient, serializer));
        var client = new TransportInfoCarrierClient(headers, serializer);

        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>().UseInfoCarrier(client).Options);

        // Started, not awaited: the gate below holds the server inside the enumeration, so this
        // task cannot complete until the test lets it.
        Task<List<Order>> query = context.Orders.OrderBy(o => o.Id).ToListAsync();

        Task reachedHeaders = await Task.WhenAny(
            headers.QueryResultReturned, Task.Delay(HeaderDeadline)).ConfigureAwait(false);

        Assert.True(
            reachedHeaders == headers.QueryResultReturned,
            "SendQueryAsync never returned while the server was still inside the row enumeration, "
            + "so the response was buffered rather than streamed.");

        // The decisive half. The header is with the client, and the server has produced *no* rows
        // -- so what the client is holding cannot have been assembled from the result set. A
        // buffered wire has no state in which this is true.
        Assert.Equal(0, gate.RowsProduced);
        Assert.False(query.IsCompleted);

        gate.Open();

        List<Order> orders = await query.ConfigureAwait(false);

        // And the streamed answer is the whole answer, which is the other half of the claim: the
        // rows are not merely early, they are all there.
        Assert.Equal(NorthwindSeed.OrderCount, orders.Count);
        Assert.Equal(NorthwindSeed.OrderCount, gate.RowsProduced);
    }

    /// <summary>
    ///     Holds the server inside its row enumeration until a test opens it.
    /// </summary>
    private sealed class RowGate
    {
        private readonly TaskCompletionSource _opened =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _rowsProduced;

        public int RowsProduced => Volatile.Read(ref this._rowsProduced);

        public Task Opened => this._opened.Task;

        public void Open() => this._opened.TrySetResult();

        public void CountRow() => Interlocked.Increment(ref this._rowsProduced);
    }

    /// <summary>
    ///     The real server, with its rows held back.
    /// </summary>
    /// <remarks>
    ///     A decorator over <see cref="QueryDataResult.Rows" /> and nothing else, so everything the
    ///     product does either side of it is the product's own behaviour. It is the streaming seam
    ///     itself that makes this possible to write: before D7 half (A) there was no point at which
    ///     a test could stand between the server producing a row and the client receiving it,
    ///     because there was no such moment.
    /// </remarks>
    private sealed class GatedServer(IInfoCarrierServer inner, RowGate gate) : IInfoCarrierServer
    {
        public async Task<QueryDataResult> QueryDataAsync(
            QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            QueryDataResult result = await inner.QueryDataAsync(request, cancellationToken).ConfigureAwait(false);
            return result with { Rows = Gated(result.Rows) };
        }

        private async IAsyncEnumerable<DynamicValueNode> Gated(IAsyncEnumerable<DynamicValueNode> rows)
        {
            // Before the first row rather than after it, so that the assertion the test makes is
            // "zero rows produced" -- a number with no other explanation.
            await gate.Opened.ConfigureAwait(false);

            await foreach (DynamicValueNode row in rows.ConfigureAwait(false))
            {
                gate.CountRow();
                yield return row;
            }
        }

        public Task<SaveChangesResult> SaveChangesAsync(
            SaveChangesRequest request, CancellationToken cancellationToken = default)
            => inner.SaveChangesAsync(request, cancellationToken);

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => inner.BeginTransactionAsync(cancellationToken);

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.CommitTransactionAsync(transactionId, cancellationToken);

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.RollbackTransactionAsync(transactionId, cancellationToken);

        public Task CreateSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.CreateSavepointAsync(transactionId, name, cancellationToken);

        public Task RollbackToSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.RollbackToSavepointAsync(transactionId, name, cancellationToken);

        public Task ReleaseSavepointAsync(string transactionId, string name, CancellationToken cancellationToken = default)
            => inner.ReleaseSavepointAsync(transactionId, name, cancellationToken);

        public Task<bool> SupportsSavepointsAsync(string transactionId, CancellationToken cancellationToken = default)
            => inner.SupportsSavepointsAsync(transactionId, cancellationToken);
    }

    /// <summary>
    ///     Signals the moment <see cref="IInfoCarrierTransport.SendQueryAsync" /> hands back a
    ///     result — that is, the moment the client has the response header and nothing more.
    /// </summary>
    /// <remarks>
    ///     <b>A transport decorator rather than a <see cref="DelegatingHandler" />, and the
    ///     difference is the whole test.</b> <c>HttpCompletionOption</c> takes effect in
    ///     <see cref="HttpClient" /> after the handler pipeline has returned, so a handler cannot
    ///     tell a buffered response from a streamed one. This sits above the option, where the two
    ///     differ.
    /// </remarks>
    private sealed class HeaderTimingTransport(IInfoCarrierTransport inner) : IInfoCarrierTransport
    {
        private readonly TaskCompletionSource _returned =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task QueryResultReturned => this._returned.Task;

        public Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => inner.SendAsync(request, cancellationToken);

        public async Task<QueryDataResult> SendQueryAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
        {
            QueryDataResult result = await inner.SendQueryAsync(request, cancellationToken).ConfigureAwait(false);
            this._returned.TrySetResult();
            return result;
        }
    }
}
