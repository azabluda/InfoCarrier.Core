// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     A write over the wire touches the columns the client changed and no others.
/// </summary>
/// <remarks>
///     <para>
///         <b>Found by comparing the server's SQL with EF's, 2026-09-15.</b> Every
///         <c>ComplexCollectionJsonUpdateSqliteTest</c> expects <c>SET "Contacts" = @p0</c> for a
///         change to one JSON column, and the server wrote <c>SET "Contacts" = @p0, "Department" = @p1,
///         "Employees" = @p2</c>. The answers were right, because nobody else was writing.
///     </para>
///     <para>
///         <b>What it costs is someone else's change.</b> A column written although the client did
///         not change it is written with the value the client loaded, so a concurrent change to it
///         is overwritten and nothing reports it. These tests make that visible: another context
///         changes a complex value between this client's read and its write.
///     </para>
/// </remarks>
public class PartialUpdateTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(PartialUpdateContext),
                OnModelCreating = (_, _) => { },
            });

    private static Task SeedAsync(SqliteInfoCarrierBackendTestStore store)
        => store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Shipment
                {
                    Id = 1,
                    Name = "original",
                    Destination = new Destination { City = "Oslo", Street = "Main" },
                    Stops = [new Stop { Place = "Bergen" }],
                });
                await context.SaveChangesAsync();
            });

    private static PartialUpdateContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<PartialUpdateContext>().UseInfoCarrier(store).Options);

    [ConditionalFact]
    public async Task A_complex_value_in_columns_that_the_client_did_not_change_is_not_written()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using PartialUpdateContext client = CreateClient(store);
        Shipment shipment = await client.Shipments.SingleAsync();

        await using (DbContext other = store.CreateDbContext())
        {
            Shipment theirs = await other.Set<Shipment>().SingleAsync();
            theirs.Destination.City = "Trondheim";
            await other.SaveChangesAsync();
        }

        shipment.Name = "mine";
        await client.SaveChangesAsync();

        await using DbContext server = store.CreateDbContext();
        Shipment stored = await server.Set<Shipment>().SingleAsync();
        Assert.Equal("mine", stored.Name);
        Assert.Equal("Trondheim", stored.Destination.City);
    }

    [ConditionalFact]
    public async Task A_complex_collection_in_json_that_the_client_did_not_change_is_not_written()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using PartialUpdateContext client = CreateClient(store);
        Shipment shipment = await client.Shipments.SingleAsync();

        await using (DbContext other = store.CreateDbContext())
        {
            Shipment theirs = await other.Set<Shipment>().SingleAsync();
            theirs.Stops.Add(new Stop { Place = "Stavanger" });
            await other.SaveChangesAsync();
        }

        shipment.Name = "mine";
        await client.SaveChangesAsync();

        await using DbContext server = store.CreateDbContext();
        Shipment stored = await server.Set<Shipment>().SingleAsync();
        Assert.Equal("mine", stored.Name);
        Assert.Equal(["Bergen", "Stavanger"], stored.Stops.Select(s => s.Place));
    }

    /// <summary>
    ///     A change to a complex collection alone, with no scalar beside it.
    /// </summary>
    /// <remarks>
    ///     The case the server's flag handling can lose: EF turns an entry unchanged when a cleared flag
    ///     leaves no scalar flag set, and its public setter for a collection reaches every element of
    ///     the entity. Six <c>ComplexCollectionJsonUpdate</c> tests wrote nothing while the first
    ///     version of the fix got that wrong.
    /// </remarks>
    [ConditionalFact]
    public async Task A_complex_collection_the_client_changed_alone_is_written()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using (PartialUpdateContext client = CreateClient(store))
        {
            Shipment shipment = await client.Shipments.SingleAsync();
            shipment.Stops.Add(new Stop { Place = "Tromso" });
            await client.SaveChangesAsync();
        }

        await using DbContext server = store.CreateDbContext();
        Shipment stored = await server.Set<Shipment>().SingleAsync();
        Assert.Equal("original", stored.Name);
        Assert.Equal(["Bergen", "Tromso"], stored.Stops.Select(s => s.Place));
    }

    [ConditionalFact]
    public async Task A_complex_value_the_client_did_change_is_written()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await SeedAsync(store);

        await using (PartialUpdateContext client = CreateClient(store))
        {
            Shipment shipment = await client.Shipments.SingleAsync();
            shipment.Destination.Street = "Harbour";
            shipment.Stops.Add(new Stop { Place = "Tromso" });
            await client.SaveChangesAsync();
        }

        await using DbContext server = store.CreateDbContext();
        Shipment stored = await server.Set<Shipment>().SingleAsync();
        Assert.Equal("Oslo", stored.Destination.City);
        Assert.Equal("Harbour", stored.Destination.Street);
        Assert.Equal(["Bergen", "Tromso"], stored.Stops.Select(s => s.Place));
    }
}
