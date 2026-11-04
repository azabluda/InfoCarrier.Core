// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.CompatibilityProbe;

// The same application runs with either the preserved product assemblies or current references.
// Rebuilding this application does not turn it into published-client acceptance evidence.
internal static class Acceptance
{
    private static int Version => InfoCarrierEnvelope.CurrentProtocolVersion;
    internal static async Task Run(string endpoint)
    {
        using var http = new HttpClient();
        IInfoCarrierClient client = InfoCarrierClient.Create(http, options => options.WithEndpoint(endpoint));
        var options = new DbContextOptionsBuilder<ProbeContext>().UseInfoCarrier(client).Options;
        await using var db = new ProbeContext(options);
        string marker = "acceptance-" + Guid.NewGuid().ToString("N");
        ProbeItem[] items = Enumerable.Range(0, 64)
            .Select(index => new ProbeItem { Name = marker + index, Number = index - 32 }).ToArray();
        db.AddRange(items);
        Check(await db.SaveChangesAsync() == items.Length, "insert count");
        Check(items.All(item => item.Id > 0) && items.Select(item => item.Id).Distinct().Count() == items.Length,
            "generated-key correlation");
        int[] keys = items.Select(item => item.Id).ToArray();
        ProbeItem[] stored = await Snapshot();
        Check(stored.Where(item => keys.Contains(item.Id)).Select(item => (item.Name, item.Number))
            .SequenceEqual(items.Select(item => (item.Name, item.Number))), "database values after insert");

        db.ChangeTracker.Clear();
        int[] expected = stored.Where(item => keys.Contains(item.Id) && item.Number >= -4)
            .OrderByDescending(item => item.Number).Skip(2).Take(5).Select(item => item.Number * 3 + 1).ToArray();
        int[] buffered = db.Items.Where(item => keys.Contains(item.Id) && item.Number >= -4)
            .OrderByDescending(item => item.Number).Skip(2).Take(5).Select(item => item.Number * 3 + 1).ToArray();
        Check(buffered.SequenceEqual(expected), "filter/order/page/arithmetic database parity");
        var streamed = new List<int>();
        await foreach (int value in db.Items.Where(item => keys.Contains(item.Id) && item.Number >= -4)
            .OrderByDescending(item => item.Number).Skip(2).Take(5).Select(item => item.Number * 3 + 1).AsAsyncEnumerable())
        {
            streamed.Add(value);
        }
        Check(streamed.SequenceEqual(expected), "streamed database parity");
        ProbeItem tracked = await db.Items.SingleAsync(item => item.Id == keys[0]);
        Check(ReferenceEquals(tracked, await db.Items.SingleAsync(item => item.Id == keys[0])), "tracking identity");

        // An older client retains its local grouping even when the server uses current rules.
        // Conversion cannot discover or repair an operator absent from the transmitted query.
        var groups = db.Items.Where(item => keys.Contains(item.Id))
            .GroupBy(item => new LocalKey(item.Number % 2)).Select(group => group.Count());
        if (Version == 3)
        {
            Check(groups.ToArray().Order().SequenceEqual(items.GroupBy(item => item.Number % 2)
                .Select(group => group.Count()).Order()), "historical local grouping retained");
        }
        else
        {
            await Refuses(() => Task.Run(() => groups.ToArray()), "implicit client execution", "current implicit grouping");
        }
        Check(db.Items.Where(item => keys.Contains(item.Id)).AsEnumerable()
            .GroupBy(item => new LocalKey(item.Number % 2)).Sum(group => group.Count()) == items.Length,
            "explicit local grouping");
        await Refuses(() => db.Items.Where(item => item.Name.StartsWith(marker, StringComparison.Ordinal)).ToArrayAsync(),
            "could not be translated", "backend refusal");

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            tracked.Number = 900;
            await db.SaveChangesAsync();
            await transaction.CreateSavepointAsync("saved");
            tracked.Number = 901;
            await db.SaveChangesAsync();
            await transaction.RollbackToSavepointAsync("saved");
            await transaction.ReleaseSavepointAsync("saved");
            await transaction.CommitAsync();
        }
        Check((await Snapshot()).Single(item => item.Id == tracked.Id).Number == 900, "savepoint commit database value");
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            tracked.Number = 902;
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        Check((await Snapshot()).Single(item => item.Id == tracked.Id).Number == 900, "rollback database value");
        db.ChangeTracker.Clear();
        var duplicate = new ProbeItem { Id = keys[0], Name = marker + "duplicate", Number = 999 };
        var atomic = new ProbeItem { Name = marker + "atomic", Number = 999 };
        db.AddRange(atomic, duplicate);
        try { await db.SaveChangesAsync(); throw new InvalidOperationException("Duplicate insert unexpectedly succeeded."); }
        catch (DbUpdateException failure)
        {
            Check(failure.Entries.Any(entry => ReferenceEquals(entry.Entity, duplicate)), "failed-entry identity");
        }
        stored = await Snapshot();
        Check(stored.All(item => item.Name != atomic.Name), "failed save atomicity");
        Check(stored.Single(item => item.Id == keys[0]).Number == 900, "failed save preserved existing row");
        db.ChangeTracker.Clear();

        // Dispose one stream early, then cancel another before execution. A new query must still work.
        await using (var enumerator = db.Items.AsNoTracking().Where(item => keys.Contains(item.Id))
            .OrderBy(item => item.Id).AsAsyncEnumerable().GetAsyncEnumerator())
        {
            Check(await enumerator.MoveNextAsync(), "partial stream yielded a row");
        }
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            try
            {
                await db.Items.Where(item => keys.Contains(item.Id)).ToArrayAsync(cancelled.Token);
                throw new InvalidOperationException("Cancelled query unexpectedly succeeded.");
            }
            catch (OperationCanceledException) { }
        }
        Check(await db.Items.CountAsync(item => keys.Contains(item.Id)) == items.Length, "query after stream disposal/cancellation");
        db.RemoveRange(await db.Items.Where(item => keys.Contains(item.Id)).ToArrayAsync());
        Check(await db.SaveChangesAsync() == items.Length, "delete count");
        Check((await Snapshot()).All(item => !keys.Contains(item.Id)), "database deletion");
        Console.WriteLine($"PASS extended acceptance protocol={InfoCarrierEnvelope.CurrentProtocolVersion}; rows={items.Length}");

        async Task<ProbeItem[]> Snapshot() => await http.GetFromJsonAsync<ProbeItem[]>(new Uri(new Uri(endpoint), "/snapshot"))
            ?? throw new InvalidOperationException("Missing independent database snapshot.");
    }

    private static async Task Refuses(Func<Task> action, string diagnostic, string check)
    {
        try { await action(); }
        catch (InvalidOperationException failure) when (failure.Message.Contains(diagnostic, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("Expected refusal: " + check);
    }

    private static void Check(bool condition, string check)
    {
        if (!condition) { throw new InvalidOperationException("Compatibility check failed: " + check); }
        Console.WriteLine("PASS " + check);
    }

    private sealed record LocalKey(int Value);
}
