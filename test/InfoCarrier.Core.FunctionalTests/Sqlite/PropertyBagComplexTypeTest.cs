// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     An entity whose complex property is a property bag holding a list crosses the wire both ways.
/// </summary>
/// <remarks>
///     <para>
///         <b>The defect these pin, #52.</b> EF builds such an entity through a branch of
///         <c>StructuralTypeMaterializerSource</c> that reads a primitive-collection member through
///         <c>Expression.Property</c>, and a property bag's members are the <c>Item[string]</c>
///         indexer, so .NET refuses it: <c>Incorrect number of arguments supplied for call to method
///         'System.Object get_Item(System.String)'</c> (<c>docs/upstream-defects.md</c> 1.1). EF's own
///         suites construct the object and never materialize it from values; this provider must,
///         because an entity reaches the other side as values.
///     </para>
/// </remarks>
public class PropertyBagComplexTypeTest
{
    private static SqliteInfoCarrierBackendTestStore CreateStore()
        => new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(PropertyBagContext),
                OnModelCreating = (_, _) => { },
            });

    private static PropertyBagContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<PropertyBagContext>().UseInfoCarrier(store).Options);

    private static Crew NewCrew()
        => new()
        {
            Id = 1,
            Name = "Quiz night",
            Team = new Dictionary<string, object>
            {
                ["Name"] = "Clueless",
                ["Members"] = new List<string> { "Boris", "David" },
            },
        };

    [ConditionalFact]
    public async Task An_entity_whose_complex_property_is_a_property_bag_holding_a_list_is_inserted()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);

        await using (PropertyBagContext client = CreateClient(store))
        {
            client.Crews.Add(NewCrew());
            await client.SaveChangesAsync();
        }

        // Read back with SQL, because EF's own query of this entity fails: see the test below.
        await using DbContext server = store.CreateDbContext();
        IEntityType crew = server.Model.FindEntityType(typeof(Crew))!;
        IComplexType team = crew.FindComplexProperty(nameof(Crew.Team))!.ComplexType;

        Assert.Equal("Quiz night", await ReadColumnAsync(server, crew, crew.FindProperty(nameof(Crew.Name))!));
        Assert.Equal("Clueless", await ReadColumnAsync(server, crew, team.FindProperty("Name")!));
        Assert.Equal("[\"Boris\",\"David\"]", await ReadColumnAsync(server, crew, team.FindProperty("Members")!));
    }

    private static Task<string> ReadColumnAsync(DbContext context, IEntityType entityType, IProperty property)
    {
        var table = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
        string sql = "SELECT \"" + property.GetColumnName(table) + "\" AS \"Value\" FROM \"" + table.Name + "\"";
        return context.Database.SqlQueryRaw<string>(sql).SingleAsync();
    }

    /// <summary>
    ///     Reading such an entity fails where plain EF Core 10 fails, with EF's own exception.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Plain EF cannot read it either, measured 2026-09-29.</b> EF's query shaper builds
    ///         the entity through the same <c>StructuralTypeMaterializerSource</c> branch, so the
    ///         server's own query of it fails before anything crosses the wire, and this client relays
    ///         that failure. When EF fixes the branch this test goes red, and the read should then
    ///         work through this client as well.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public async Task Reading_such_an_entity_fails_where_EF_Core_10_fails()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(NewCrew());
                await context.SaveChangesAsync();
            });

        ArgumentException directly;
        await using (DbContext server = store.CreateDbContext())
        {
            directly = await Assert.ThrowsAsync<ArgumentException>(() => server.Set<Crew>().SingleAsync());
        }

        await using PropertyBagContext client = CreateClient(store);
        ArgumentException overTheWire = await Assert.ThrowsAsync<ArgumentException>(() => client.Crews.SingleAsync());

        Assert.StartsWith(
            "Incorrect number of arguments supplied for call to method 'System.Object get_Item(System.String)'",
            directly.Message,
            StringComparison.Ordinal);
        Assert.Equal(directly.Message, overTheWire.Message);
    }
}
