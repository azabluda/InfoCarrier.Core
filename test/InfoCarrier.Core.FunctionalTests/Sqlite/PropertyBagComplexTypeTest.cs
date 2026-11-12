// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

    [Fact]
    public async Task An_entity_whose_complex_property_is_a_property_bag_holding_a_list_is_inserted()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);

        await using (PropertyBagContext client = CreateClient(store))
        {
            client.Crews.Add(NewCrew());
            await client.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Check the stored representation independently of both materializers.
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

    /// <summary>Property-bag complex values can be read directly and through the wire.</summary>
    [Fact]
    public async Task Reading_a_property_bag_complex_value_matches_the_direct_query()
    {
        await using SqliteInfoCarrierBackendTestStore store = CreateStore();
        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(NewCrew());
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            });

        await using DbContext server = store.CreateDbContext();
        Crew directly = await server.Set<Crew>().SingleAsync(TestContext.Current.CancellationToken);
        await using PropertyBagContext client = CreateClient(store);
        Crew overTheWire = await client.Crews.SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(directly.Name, overTheWire.Name);
        Assert.Equal(directly.Team["Name"], overTheWire.Team["Name"]);
        Assert.Equal(
            Assert.IsType<List<string>>(directly.Team["Members"]),
            Assert.IsType<List<string>>(overTheWire.Team["Members"]));
    }
}
