// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Northwind.Shared;
using Northwind.Shared.Model;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ServerCancellationOverSocketTest
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Cancelling_a_query_over_a_real_socket_stops_server_execution()
    {
        var probe = new QueryCancellationProbe();
        await using var factory = new NorthwindServerFactory();
        await using WebApplicationFactory<Program> hosted = factory.WithWebHostBuilder(
            builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton<IInterceptor>(probe);
                services.AddSingleton<IStartupFilter>(probe);
            }));
        hosted.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

        using HttpClient httpClient = hosted.CreateClient();
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        httpClient.DefaultRequestVersion = HttpVersion.Version11;
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        var serializer = new SystemTextJsonInfoCarrierSerializer();
        var client = new TransportInfoCarrierClient(
            new HttpInfoCarrierTransport(httpClient, serializer), serializer);
        await using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>().UseInfoCarrier(client).Options);

        // A real query succeeds first. Host startup and database seeding must not arm the probe.
        List<Customer> customers = await context.Customers.Where(c => c.Country == "Germany")
            .ToListAsync().WaitAsync(Deadline);
        Assert.NotEmpty(customers);

        using var cancellation = new CancellationTokenSource();
        probe.Arm();
        Task<List<Customer>> query = context.Customers.Where(c => c.Country == "Germany")
            .ToListAsync(cancellation.Token);

        try
        {
            await WaitForAsync(probe.CommandEntered.Task, "the server command to start");
            Assert.False(probe.CancellationObserved.Task.IsCompleted, "The server cancelled before the client did.");
            Assert.False(probe.RequestFinished.Task.IsCompleted, "The server request ended before cancellation.");

            await cancellation.CancelAsync();

            // The client stopping is not enough: the command must observe its server-side token,
            // and the actual request pipeline must unwind before any cleanup releases the probe.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.WaitAsync(Deadline));
            await WaitForAsync(probe.CancellationObserved.Task, "server-side command cancellation");
            await WaitForAsync(probe.RequestFinished.Task, "the server request to finish");
        }
        finally
        {
            // Also release an uncancellable command during a failed mutation check. Host shutdown
            // must never be what cancels the command and accidentally satisfies the assertions.
            probe.Release();
            await cancellation.CancelAsync();
            try
            {
                await query.WaitAsync(Deadline);
            }
            catch (OperationCanceledException)
            {
                // Expected after client cancellation; observe the task before disposing its context.
            }
        }
    }

    private static async Task WaitForAsync(Task signal, string description)
    {
        try
        {
            await signal.WaitAsync(Deadline);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"Timed out waiting for {description}. The deadline only prevents a hung test.");
        }
    }

    // Keep the real endpoint, envelope dispatch, query executor and Entity Framework command path.
    // Only the command boundary waits on a signal, so no sleep or slow database query orders events.
    // This proves cancellation reaches server execution, not native cancellation in every database.
    private sealed class QueryCancellationProbe : DbCommandInterceptor, IStartupFilter
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private volatile bool _armed;

        public TaskCompletionSource CommandEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource RequestFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Arm() => _armed = true;

        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (!_armed)
            {
                return result;
            }

            this.CommandEntered.TrySetResult();
            try
            {
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                this.CancellationObserved.TrySetResult();
                throw;
            }

            return result;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => application =>
            {
                application.Use(async (http, nextMiddleware) =>
                {
                    // Capture at entry: completion of the earlier control query cannot signal
                    // completion of the request being measured, even if its final unwind is late.
                    bool measuredRequest = _armed && http.Request.Path == "/infocarrier";
                    try
                    {
                        await nextMiddleware(http).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (measuredRequest)
                        {
                            this.RequestFinished.TrySetResult();
                        }
                    }
                });
                next(application);
            };
    }
}
