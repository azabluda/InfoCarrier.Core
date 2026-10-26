// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using InfoCarrier.Core.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

[Collection(nameof(RoundTripMetricsCollection))]
public class NamedCancellationIsolationTest
{
    [Fact]
    public async Task Cancelling_one_named_query_does_not_cancel_another_endpoint()
    {
        TimeSpan deadline = TimeSpan.FromSeconds(15);
        string path = Path.Combine(Path.GetTempPath(), $"icc-cancellation-{Guid.NewGuid():N}.db");
        var first = new CommandGate();
        var second = new CommandGate();
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        WebApplication? app = null;
        Add("first", first);
        Add("second", second);
        app = builder.Build();
        try
        {
            app.MapInfoCarrier("/first", serverName: "first");
            app.MapInfoCarrier("/second", serverName: "second");
            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                Context seed = scope.ServiceProvider.GetRequiredKeyedService<Context>("first");
                await seed.Database.EnsureCreatedAsync();
                seed.Add(new Item { Id = 1, Name = "stored" });
                await seed.SaveChangesAsync();
            }
            await app.StartAsync();
            using var meter = new RegistrationMetricsTest.Recording();
            await using Context firstClient = Remote("first");
            await using Context secondClient = Remote("second");
            using var cancellation = new CancellationTokenSource();
            first.Arm();
            second.Arm();
            Task<List<Item>> cancelled = firstClient.Items.ToListAsync(cancellation.Token);
            Task<List<Item>> surviving = secondClient.Items.ToListAsync();
            try
            {
                await Task.WhenAll(first.Entered.Task, second.Entered.Task).WaitAsync(deadline);
                await cancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(deadline));
                await first.Cancelled.Task.WaitAsync(deadline);
                Assert.False(second.Cancelled.Task.IsCompleted);
                Assert.False(surviving.IsCompleted);
                second.Release();
                Assert.Equal("stored", Assert.Single(await surviving.WaitAsync(deadline)).Name);
                // A cancelled request must not poison the registration or its next operation.
                Assert.Equal("stored", Assert.Single(await firstClient.Items.ToListAsync().WaitAsync(deadline)).Name);
                await meter.WaitForPairs(3, 3);
                meter.AssertPairs(3, 3);
                foreach (string instrument in new[]
                {
                    InfoCarrierMetrics.RoundTripCountInstrumentName, InfoCarrierMetrics.ServerRequestCountInstrumentName,
                })
                {
                    RegistrationMetricsTest.Reading cancelledReading = Assert.Single(meter.For(instrument),
                        reading => Equals(reading.Tags[InfoCarrierMetrics.OutcomeTagName], "cancelled"));
                    string nameTag = instrument == InfoCarrierMetrics.RoundTripCountInstrumentName
                        ? InfoCarrierMetrics.ClientNameTagName : InfoCarrierMetrics.EndpointNameTagName;
                    Assert.Equal("first", cancelledReading.Tags[nameTag]);
                    Assert.Contains(InfoCarrierMetrics.ErrorTypeTagName, cancelledReading.Tags.Keys);
                    Assert.Equal(2, meter.For(instrument).Count(reading =>
                        Equals(reading.Tags[InfoCarrierMetrics.OutcomeTagName], "success")));
                }
            }
            finally
            {
                first.Release();
                second.Release();
                await cancellation.CancelAsync();
                try { await Task.WhenAll(cancelled, surviving).WaitAsync(deadline); }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            await app.DisposeAsync();
            File.Delete(path);
            File.Delete(path + "-wal");
            File.Delete(path + "-shm");
        }

        void Add(string name, CommandGate gate)
        {
            builder.Services.AddKeyedScoped<Context>(name, (_, _) => new Context(new DbContextOptionsBuilder<Context>()
                .UseSqlite($"Data Source={path};Pooling=false").AddInterceptors(gate).Options));
            builder.Services.AddInfoCarrierServer<Context>(name, configure: null,
                sp => sp.GetRequiredKeyedService<Context>(name));
            builder.Services.AddInfoCarrierClient(name, o => o.WithEndpoint("http://localhost/" + name)
                .WithMaxResponseBytes(4096))
                .ConfigurePrimaryHttpMessageHandler(() => app!.GetTestServer().CreateHandler());
        }

        Context Remote(string name) => new(new DbContextOptionsBuilder<Context>()
            .UseApplicationServiceProvider(app!.Services).UseInfoCarrier(name).Options);
    }

    public sealed class Context(DbContextOptions<Context> options) : DbContext(options)
    {
        public DbSet<Item> Items => Set<Item>();
    }

    public sealed class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // Synchronize real server commands rather than relying on query duration or sleeps.
    private sealed class CommandGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _armed;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void Arm() => Interlocked.Exchange(ref _armed, 1);
        internal void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _armed, 0) == 0) { return result; }
            Entered.TrySetResult();
            try { await _release.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
            return result;
        }
    }
}
