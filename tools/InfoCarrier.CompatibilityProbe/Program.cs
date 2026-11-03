// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Wire;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.CompatibilityProbe;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args is ["metrics", string number] && int.TryParse(number, out int measuredVersion) && measuredVersion is 3 or 4)
        {
            Measure(measuredVersion);
            return;
        }
        if (args is ["verify-empty", string databasePath])
        {
            await using var stored = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("Data Source=" + databasePath).Options);
            if (await stored.Items.AnyAsync()) { throw new InvalidOperationException("A refused client changed stored data."); }
            Console.WriteLine("PASS refused client had zero database effects");
            return;
        }
        if (args is ["server", string address, string database])
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(address);
            builder.Services.AddDbContext<ProbeContext>(o => o.UseSqlite("Data Source=" + database));
            builder.Services.AddInfoCarrierServer<ProbeContext>();
            await using WebApplication app = builder.Build();
            using (IServiceScope scope = app.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ProbeContext>();
                await context.Database.EnsureCreatedAsync();
            }
            app.MapInfoCarrier();
            app.MapGet("/ready", () => "ready");
            await app.RunAsync();
            return;
        }
        if (args is not ["client", string endpoint])
        {
            throw new ArgumentException("Usage: server <address> <database> | client <endpoint>");
        }

        using var http = new HttpClient();
        IInfoCarrierClient client = InfoCarrierClient.Create(http, o => o.WithEndpoint(endpoint));
        var options = new DbContextOptionsBuilder<ProbeContext>().UseInfoCarrier(client).Options;
        await using var db = new ProbeContext(options);
        string marker = "client-" + Guid.NewGuid().ToString("N");
        var item = new ProbeItem { Name = marker, Number = 42 };
        db.Add(item);
        await db.SaveChangesAsync();
        if (item.Id == 0) { throw new InvalidOperationException("Generated key was not returned."); }
        int key = item.Id;
        db.ChangeTracker.Clear();
        ProbeItem buffered = db.Items.Single(x => x.Id == key);
        if (buffered.Name != marker || buffered.Number != 42) { throw new InvalidOperationException("Buffered query changed values."); }
        var rows = new List<ProbeItem>();
        await foreach (ProbeItem row in db.Items.AsNoTracking().Where(x => x.Id == key).AsAsyncEnumerable())
        {
            rows.Add(row);
        }
        if (rows.Count != 1 || rows[0].Name != marker) { throw new InvalidOperationException("Stream changed values."); }
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            buffered.Number = 99;
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        if (await db.Items.Where(x => x.Id == key).Select(x => x.Number).SingleAsync() != 42)
        {
            throw new InvalidOperationException("Rollback did not preserve the stored value.");
        }
        ProbeItem removed = await db.Items.SingleAsync(x => x.Id == key);
        db.Remove(removed);
        await db.SaveChangesAsync();
        if (await db.Items.AnyAsync(x => x.Id == key)) { throw new InvalidOperationException("Delete did not reach the database."); }
        Console.WriteLine($"PASS protocol={InfoCarrierEnvelope.CurrentProtocolVersion}; buffered; streaming; generated-key; rollback; delete");
    }

    private static void Measure(int selectedVersion)
    {
        using ServiceProvider services = new ServiceCollection().AddInfoCarrierWireProtocols().BuildServiceProvider();
        InfoCarrierWireProtocol protocol = services.GetRequiredService<InfoCarrierWireProtocol>();
        using var model = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite("Data Source=:memory:").Options);
        var request = new QueryDataRequest
        {
            Query = ExpressionSerializer.CreateForModel(model.Model).ToNode(model.Items.Where(item => item.Number > 42).Expression),
            TrackingBehavior = QueryTrackingBehavior.NoTracking, IsAsync = true, ReturnsSingleResult = false,
        };
        var serializer = new SystemTextJsonInfoCarrierSerializer(InfoCarrierPayloadLimits.Default, protocol);
        foreach (int version in new[] { selectedVersion })
        {
            request = request with { ProtocolVersion = version };
            long initial = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.StartNew();
            byte[] first = Cycle();
            timer.Stop();
            Console.WriteLine($"Protocol {version}: request bytes={first.Length}; first encode/decode allocated={GC.GetAllocatedBytesForCurrentThread() - initial}; elapsed={timer.Elapsed.TotalMilliseconds:F2} ms.");
            initial = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            for (int index = 0; index < 1000; index++) { GC.KeepAlive(Cycle()); }
            timer.Stop();
            Console.WriteLine($"Protocol {version}: 1000 warm encode/decode cycles allocated={GC.GetAllocatedBytesForCurrentThread() - initial}; elapsed={timer.Elapsed.TotalMilliseconds:F2} ms.");

            byte[] Cycle()
            {
                byte[] bytes = protocol.Serialize(serializer, request, version);
                QueryDataRequest decoded = protocol.Deserialize<QueryDataRequest>(serializer, bytes, version)
                    ?? throw new InvalidOperationException("The measured request disappeared.");
                GC.KeepAlive(decoded.Query);
                return bytes;
            }
        }
    }
}

/// <summary>Shared model used by the separate-process compatibility probe.</summary>
public sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
{
    /// <summary>Gets the probe items.</summary>
    public DbSet<ProbeItem> Items => Set<ProbeItem>();
}

/// <summary>One row whose generated key and mutable values cross the wire.</summary>
public sealed class ProbeItem
{
    /// <summary>Gets or sets the generated key.</summary>
    public int Id { get; set; }
    /// <summary>Gets or sets the client marker.</summary>
    public required string Name { get; set; }
    /// <summary>Gets or sets the mutable value.</summary>
    public int Number { get; set; }
}
