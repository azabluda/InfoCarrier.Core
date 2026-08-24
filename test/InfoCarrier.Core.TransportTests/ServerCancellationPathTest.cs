// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

/// <summary>
///     Records which command path EF took on the server, and whether the token it was given could
///     ever be cancelled.
/// </summary>
/// <remarks>
///     <para>
///         <b>The sync/async distinction is the whole instrument.</b> A synchronous enumeration of
///         the query reaches <see cref="ReaderExecuting" />; an asynchronous one reaches
///         <see cref="ReaderExecutingAsync" /> and carries a <see cref="CancellationToken" /> that
///         EF hands to the <see cref="DbCommand" />. Only the second can be interrupted, so which
///         method fires is a direct reading of whether a cancelling client can stop the store.
///     </para>
/// </remarks>
public sealed class CommandPathRecorder : DbCommandInterceptor
{
    public int SyncReaderCalls { get; private set; }

    public int AsyncReaderCalls { get; private set; }

    public bool AsyncTokenCanBeCanceled { get; private set; }

    /// <summary>
    ///     Forgets everything recorded so far.
    /// </summary>
    /// <remarks>
    ///     Starting the host seeds the store, and seeding is synchronous, so a run that counted
    ///     from construction saw the seed's own reader calls and could not tell them from the
    ///     query's. Reset immediately before the query being measured.
    /// </remarks>
    public void Reset()
    {
        this.SyncReaderCalls = 0;
        this.AsyncReaderCalls = 0;
        this.AsyncTokenCanBeCanceled = false;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        this.SyncReaderCalls++;
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        this.AsyncReaderCalls++;
        this.AsyncTokenCanBeCanceled = cancellationToken.CanBeCanceled;
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

/// <summary>
///     Asserts that a query arriving over HTTP reaches the store on the cancellable path.
/// </summary>
/// <remarks>
///     <para>
///         <b>This test exists because three earlier designs for it could not have failed.</b>
///         Each replaced the server with a fake that waited on a token, which removed
///         <c>ServerQueryExecutor</c> from the path being measured, and
///         <c>ServerQueryExecutor</c> is where the defect was. A test that cannot reach the code
///         it is about is not evidence, however end-to-end it looks.
///     </para>
///     <para>
///         <b>Nothing here cancels anything, and that is deliberate.</b> Cancelling mid-flight
///         needs the server to be slow, which means either a sleep or a race, and this repository
///         treats a flaky test as a stop-everything defect. The reading taken instead is the one
///         that distinguishes the two implementations without any timing at all: the old server
///         enumerated the query synchronously, so EF called <c>ReaderExecuting</c> and no token
///         reached the command; the current one enumerates asynchronously, so EF calls
///         <c>ReaderExecutingAsync</c> and hands the command a token that can be cancelled.
///     </para>
///     <para>
///         What it does not cover is Kestrel: <c>WebApplicationFactory</c> runs the pipeline in
///         memory, with no socket and no port, so whether a real web server reports a lost client
///         promptly for a POST is a separate question (`implementation-plan.md`, Q2).
///     </para>
/// </remarks>
public class ServerCancellationPathTest(NorthwindServerFactory factory) : IClassFixture<NorthwindServerFactory>
{
    [Fact]
    public async Task A_query_over_http_reaches_the_store_on_the_cancellable_path()
    {
        var recorder = new CommandPathRecorder();

        using WebApplicationFactory<Program> hosted = factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(services => services.AddSingleton<IInterceptor>(recorder)));

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(hosted.CreateClient(), serializer), serializer);

        await using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>().UseInfoCarrier(client).Options);

        // Everything before this line is setup, and starting the host seeds the store on the
        // synchronous path. Only the query below is being measured.
        recorder.Reset();

        List<Customer> customers = await context.Customers.Where(c => c.Country == "Germany").ToListAsync();
        Assert.NotEmpty(customers);

        // The server read the store, and it read it on the path that carries a token.
        Assert.True(recorder.AsyncReaderCalls > 0, "the server did not use EF's async command path");
        Assert.True(recorder.AsyncTokenCanBeCanceled, "the command was given a token that can never be cancelled");

        // And it used no other path. Without this, a server that ran the query twice, once each
        // way, would still satisfy the assertions above.
        Assert.Equal(0, recorder.SyncReaderCalls);
    }
}
