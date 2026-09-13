// THROWAWAY PROBE. Deleted before commit.
// Reproduce the SIGNATURE rather than wait for the flake (R76's route): a tracking query whose
// entity does not land in the state manager.

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.DocumentStoreTests;

public abstract class ZzStressBase
{
    protected static async Task HammerAsync(DocumentStoreFixture fixture, string label)
    {
        var empty = 0;
        var wrong = 0;

        for (var i = 0; i < 200; i++)
        {
            await using ShopClientContext ctx = fixture.CreateClient();
            Customer bob = await ctx.Customers.SingleAsync(c => c.Id == "bob");
            bob.Name = "Robert" + i;
            ctx.ChangeTracker.DetectChanges();

            int tracked = ctx.ChangeTracker.Entries().Count();
            if (tracked == 0)
            {
                empty++;
            }
            else if (tracked != 4)
            {
                wrong++;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "probe-stress.txt"),
                    $"{label} iteration {i} tracked={tracked}: " + string.Join(
                        " | ",
                        ctx.ChangeTracker.Entries().Select(
                            e => e.Metadata.ShortName() + ":" + e.State + ":"
                                + string.Join(",", e.Properties.Where(p => p.Metadata.IsKey())
                                    .Select(p => p.Metadata.Name + "=" + p.CurrentValue))))
                    + Environment.NewLine);
            }
        }

        System.IO.File.AppendAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "probe-stress.txt"),
            $"{label} DONE empty={empty} wrong={wrong}{Environment.NewLine}");

        Assert.Equal(0, empty);
        Assert.Equal(0, wrong);
    }
}

public class ZzStressA(DocumentStoreFixture fixture) : ZzStressBase, IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public Task Hammer() => HammerAsync(fixture, "A");
}

public class ZzStressB(DocumentStoreFixture fixture) : ZzStressBase, IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public Task Hammer() => HammerAsync(fixture, "B");
}

public class ZzStressC(DocumentStoreFixture fixture) : ZzStressBase, IClassFixture<DocumentStoreFixture>
{
    [Fact]
    public Task Hammer() => HammerAsync(fixture, "C");
}
