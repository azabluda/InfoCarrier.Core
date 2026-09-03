// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     What happens when the client's model and the server's model disagree about whether a
///     member is mapped.
/// </summary>
/// <remarks>
///     <para>
///         <b>The client's model is the authority, and until R139 nothing enforced that.</b>
///         <c>ServerBoundaryAnalyzer</c> admitted every <c>MemberExpression</c> without asking the
///         model whether the member is mapped. Where the server's model happened to map it, the
///         query was translated and answered — a query EF Core refuses on every other provider
///         returned data instead.
///     </para>
///     <para>
///         <b>It was invisible for eight milestones because nothing could make the two models
///         disagree.</b> Every fixture builds both from one <c>OnModelCreating</c>. R138 found it
///         by accident: mapping ten <c>Order</c> properties on the Northwind server model to give
///         the store its real schema made four spec tests that assert
///         <c>QueryUnableToTranslateMember</c> stop throwing.
///         <see cref="Shipment" /> is the permanent version of that experiment.
///     </para>
///     <para>
///         <b>The rule this pins is not "refuse unmapped members".</b> It is that the CLIENT model
///         decides. A member the client does not map cannot be translated by this provider, so it
///         falls to the same treatment every other untranslatable construct gets: refused in a
///         predicate, and evaluated on the client in a final projection, which is the one piece of
///         client-side work this provider allows.
///     </para>
/// </remarks>
public class UnmappedMemberBoundaryTest
{
    /// <summary>
    ///     A predicate on a member the client model does not map is refused, not answered.
    /// </summary>
    /// <remarks>
    ///     EF Core's own answer for an untranslatable predicate, which is what
    ///     <c>QuerySplitter.RejectClientEvaluation</c> raises. Answering it instead would mean the
    ///     server had applied a filter the client could not describe, and the client had no way to
    ///     know the two models differed.
    /// </remarks>
    [ConditionalFact]
    public async Task A_predicate_on_a_member_the_client_model_does_not_map_is_refused()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext context = CreateClient(store);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Shipments.Where(s => s.Note == "urgent").ToListAsync());

        Assert.Contains(nameof(Shipment.Note), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The mapped member beside it still works, so the refusal is about the member and not
    ///     about the entity.
    /// </summary>
    [ConditionalFact]
    public async Task A_predicate_on_the_mapped_member_of_the_same_entity_still_runs()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext context = CreateClient(store);

        List<Shipment> found = await context.Shipments
            .Where(s => s.Reference == "alpha")
            .ToListAsync();

        Shipment single = Assert.Single(found);
        Assert.Equal("alpha", single.Reference);

        // Never read back, because the client's model has no such property to read it into.
        Assert.Null(single.Note);
    }

    /// <summary>
    ///     The server's model maps the member and its column holds a value, so the disagreement is
    ///     real rather than an empty column that would refuse any query anyway.
    /// </summary>
    /// <remarks>
    ///     Read through the server's own context, not over the wire. Without this the two tests
    ///     above would pass just as well against a store that never had the column, and the pin
    ///     would be asserting nothing.
    /// </remarks>
    [ConditionalFact]
    public async Task The_servers_model_maps_the_member_and_its_value_is_in_the_store()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using DbContext server = store.CreateDbContext();

        List<string?> notes = await server.Set<Shipment>()
            .Where(s => s.Reference == "alpha")
            .Select(s => s.Note)
            .ToListAsync();

        Assert.Equal("urgent", Assert.Single(notes));
    }

    /// <summary>
    ///     The control the other three rest on: the CLIENT's model really does not map the member.
    /// </summary>
    /// <remarks>
    ///     Without this, every assertion here would hold just as well against a client that maps it
    ///     and a server that does not, which is a different situation entirely.
    /// </remarks>
    [ConditionalFact]
    public async Task The_clients_model_does_not_map_the_member()
    {
        await using SqliteInfoCarrierBackendTestStore store = await SeededStoreAsync();
        await using SqliteSmokeContext context = CreateClient(store);

        Microsoft.EntityFrameworkCore.Metadata.IEntityType shipment =
            context.Model.FindEntityType(typeof(Shipment))!;

        Assert.NotNull(shipment.FindProperty(nameof(Shipment.Reference)));
        Assert.Null(shipment.FindProperty(nameof(Shipment.Note)));
    }

    /// <summary>
    ///     A store whose SERVER model maps <see cref="Shipment.Note" />, which
    ///     <see cref="SqliteSmokeContext" /> ignores for both sides.
    /// </summary>
    /// <remarks>
    ///     The model customizer runs on the server's context only, which is what lets the two
    ///     models differ at all. It is the same move
    ///     <c>NorthwindInfoCarrierSqliteServerContext</c> makes for <c>Product.CategoryID</c>:
    ///     un-ignoring a property after the context's own <c>OnModelCreating</c> has ignored it.
    /// </remarks>
    private static async Task<SqliteInfoCarrierBackendTestStore> SeededStoreAsync()
    {
        SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(SqliteSmokeContext),
                OnModelCreating = (modelBuilder, _) =>
                    modelBuilder.Entity<Shipment>().Property(e => e.Note),
            });

        await store.InitializeAsync(
            store.ServiceProvider,
            store.CreateDbContext,
            seed: async context =>
            {
                context.Add(new Shipment { Id = 1, Reference = "alpha", Note = "urgent" });
                context.Add(new Shipment { Id = 2, Reference = "beta", Note = "routine" });
                await context.SaveChangesAsync();
            });

        return store;
    }

    private static SqliteSmokeContext CreateClient(SqliteInfoCarrierBackendTestStore store)
        => new(new DbContextOptionsBuilder<SqliteSmokeContext>()
            .UseInfoCarrier(store)
            .Options);
}
