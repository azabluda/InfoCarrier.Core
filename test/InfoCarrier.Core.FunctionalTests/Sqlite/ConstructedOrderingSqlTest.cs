// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>Registered constructor ordering reaches the backend; unregistered ordering stays local.</summary>
public class ConstructedOrderingSqlTest
{
    [ConditionalTheory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public async Task Constructor_ordering_separates_registered_refusal_from_unregistered_local_ordering(bool async, bool constructor, bool registered)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(CityContext),
                AllowedTypes = registered ? [typeof(ConstructedItem)] : [],
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(
                new CityRow { Id = 1, City = "beta" },
                new CityRow { Id = 2, City = null },
                new CityRow { Id = 3, City = "alpha" });
            await context.SaveChangesAsync();
        });

        using DbContext server = store.CreateDbContext();
        await using CityContext client = new(new DbContextOptionsBuilder<CityContext>()
            .UseInfoCarrier(store, options => options.AllowTypes(registered ? [typeof(ConstructedItem)] : [])).Options);

        async Task<string?[]> Execute(DbContext context)
        {
            if (constructor)
            {
                var query = context.Set<CityRow>().Select(row => new ConstructedItem(row.City)).OrderBy(item => item.City);
                var rows = async ? await query.ToArrayAsync() : query.ToArray();
                return [.. rows.Select(item => item.City)];
            }
            else
            {
                var query = context.Set<CityRow>().Select(row => new { row.City }).OrderBy(item => item.City);
                var rows = async ? await query.ToArrayAsync() : query.ToArray();
                return [.. rows.Select(item => item.City)];
            }
        }

        store.ServerSql.Clear();
        if (constructor && registered)
        {
            InvalidOperationException direct = await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(server));
            Assert.Contains("could not be translated", direct.Message, StringComparison.Ordinal);
            Assert.Empty(store.ServerSql.Statements);

            InvalidOperationException carried = await Assert.ThrowsAsync<InvalidOperationException>(() => Execute(client));
            Assert.Contains("could not be translated", carried.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("QuerySplitter", carried.ToString(), StringComparison.Ordinal);
            string serverStack = Assert.IsType<string>(carried.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
            Assert.Contains("Microsoft.EntityFrameworkCore.Query", serverStack, StringComparison.Ordinal);
            Assert.Empty(store.ServerSql.Statements);
        }
        else if (constructor)
        {
            Assert.Equal(new string?[] { null, "alpha", "beta" }, await Execute(client));
            string statement = Assert.Single(store.ServerSql.Statements);
            Assert.DoesNotContain("ORDER BY", statement, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(new string?[] { null, "alpha", "beta" }, await Execute(server));
            string[] direct = [.. store.ServerSql.Statements];
            Assert.Contains("ORDER BY", Assert.Single(direct), StringComparison.Ordinal);
            store.ServerSql.Clear();
            Assert.Equal(new string?[] { null, "alpha", "beta" }, await Execute(client));
            store.AssertServerSql(direct);
        }
    }

    public sealed class ConstructedItem(string? city)
    {
        public string? City { get; } = city;
    }

    public sealed class CityRow
    {
        public int Id { get; set; }
        public string? City { get; set; }
    }

    public sealed class CityContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<CityRow>();
    }
}
