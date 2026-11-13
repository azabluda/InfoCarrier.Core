// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core.FunctionalTests;

// Separate processes exercise the real store statics and startup cleanup. Standard xUnit
// discovery/execution still goes through the normal runner.
internal static class SqliteIsolationChild
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "--sqlite-isolation-child")
        {
            return await Xunit.Runner.InProc.SystemConsole.ConsoleRunner.Run(args);
        }

        try
        {
            string marker = args[1];
            await using var store = new ProbeStore("Closed");
            await store.Seed(marker);
            // Change seeded data, so recreating or reseeding it is detectable too.
            await using (var context = store.Context())
            {
                context.Add(new ProbeRow { Id = 2, Marker = marker });
                await context.SaveChangesAsync();
            }

            await using var openStore = new ProbeStore("Open-" + marker);
            await openStore.Seed(marker);
            await using var open = openStore.CreateStoreConnection();
            await open.OpenAsync();
            await using (var command = open.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_autocheckpoint=0; UPDATE Rows SET Marker=Marker || '-open'";
                await command.ExecuteNonQueryAsync();
            }

            await using var closed = store.CreateStoreConnection();
            Console.WriteLine(JsonSerializer.Serialize(new[] { closed.DataSource, open.DataSource }));
            while (await Console.In.ReadLineAsync() is { } command)
            {
                if (command == "exit") return 0;
                if (command != "check") throw new InvalidOperationException(command);

                await using var shared = new ProbeStore("Closed");
                await shared.Seed("unexpected reseed");
                await using var context = shared.Context();
                string[] rows = await context.Rows.OrderBy(r => r.Id).Select(r => r.Marker).ToArrayAsync();
                if (!rows.SequenceEqual(new[] { marker, marker }))
                    throw new InvalidOperationException("Closed database was deleted, recreated, or reseeded: " + string.Join(",", rows));

                await using var direct = new DbContext(shared.AddDirectClientOptions(new DbContextOptionsBuilder()).Options);
                if (direct.Database.GetDbConnection().DataSource != closed.DataSource)
                    throw new InvalidOperationException("Direct comparison uses a different database path.");
                await using var read = direct.Database.GetDbConnection().CreateCommand();
                read.CommandText = "SELECT Marker FROM Rows WHERE Id=2";
                if (!Equals(await read.ExecuteScalarAsync(), marker))
                    throw new InvalidOperationException("Direct comparison uses a different database.");

                await using var openRead = open.CreateCommand();
                openRead.CommandText = "SELECT Marker FROM Rows";
                if (!Equals(await openRead.ExecuteScalarAsync(), marker + "-open"))
                    throw new InvalidOperationException("Open database changed.");
                Console.WriteLine("unchanged");
            }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    internal static async Task<(string FirstPath, string SecondPath, string FirstMarker, string SecondMarker)> LongNames()
    {
        string name = new('L', 120);
        await using var first = new ProbeStore(name, shared: false);
        await using var second = new ProbeStore(name, shared: false);
        await first.Seed("first");
        await second.Seed("second");
        await using var firstConnection = first.CreateStoreConnection();
        await using var secondConnection = second.CreateStoreConnection();
        await using var firstContext = first.Context();
        await using var secondContext = second.Context();
        return (firstConnection.DataSource, secondConnection.DataSource,
            (await firstContext.Rows.SingleAsync()).Marker, (await secondContext.Rows.SingleAsync()).Marker);
    }

    private sealed class ProbeStore(string name, bool shared = true) : SqliteInfoCarrierBackendTestStore(
        name, shared, new SharedTestStoreProperties { ContextType = typeof(ProbeContext), OnModelCreating = (_, _) => { } })
    {
        public ProbeContext Context() => new(AddProviderOptions(new DbContextOptionsBuilder()).Options);

        public Task Seed(string marker) => InitializeAsync(Context, async context =>
        {
            context.Add(new ProbeRow { Id = 1, Marker = marker });
            await context.SaveChangesAsync();
        }, null);
    }

    private sealed class ProbeContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ProbeRow> Rows => Set<ProbeRow>();
    }

    private sealed class ProbeRow
    {
        public int Id { get; set; }
        public string Marker { get; set; } = "";
    }
}
