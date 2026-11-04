// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.AspNetCore;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.CompatibilityProbe;

internal static partial class Program
{
    static partial void RunCurrentCommand(string[] args, ref bool handled);

    private static async Task Main(string[] args)
    {
        if (args is ["acceptance", string acceptanceEndpoint])
        {
            await Acceptance.Run(acceptanceEndpoint);
            return;
        }
        bool handled = false;
        RunCurrentCommand(args, ref handled);
        if (handled) { return; }
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
            app.MapGet("/snapshot", async (ProbeContext context) =>
                await context.Items.AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
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
