// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public class DistinctClientProjectionTest
{
    [ConditionalTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task A_client_projection_after_distinct_matches_the_direct_statement(bool async, bool distinct)
        => await AssertProjection(async, distinct, ordered: false);

    [ConditionalTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_client_projection_after_ordered_distinct_matches_the_direct_statement(bool async)
        => await AssertProjection(async, distinct: true, ordered: true);

    private static async Task AssertProjection(bool async, bool distinct, bool ordered)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(), shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(RowContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.AddRange(
                new Row { Id = 1, Date = new DateTime(2011, 1, 1) },
                new Row { Id = 2, Date = new DateTime(2011, 2, 1) },
                new Row { Id = 3, Date = new DateTime(2012, 1, 1) },
                new Row { Id = 20001, Date = new DateTime(2020, 1, 1) });
            await context.SaveChangesAsync();
        });
        using DbContext server = store.CreateDbContext();
        var options = new DbContextOptionsBuilder().UseInfoCarrier(store).Options;
        await using RowContext client = new(options);

        async Task<int[]> Read(DbContext context)
        {
            IQueryable<int> years = context.Set<Row>().Where(row => row.Id < 20000).Select(row => row.Date!.Value.Year);
            if (distinct) { years = years.Distinct(); }
            if (ordered) { years = years.OrderBy(year => year); }
            var query = years.Select(year => new Result { Value = NextYear(year) });
            Result[] rows = async ? await query.ToArrayAsync() : query.ToArray();
            return rows.Select(row => row.Value).Order().ToArray();
        }

        int[] expected = distinct ? [2012, 2013] : [2012, 2012, 2013];
        store.ServerSql.Clear();
        Assert.Equal(expected, await Read(server));
        string[] direct = [.. store.ServerSql.Statements];
        Assert.Single(direct);
        store.ServerSql.Clear();
        Assert.Equal(expected, await Read(client));
        store.AssertServerSql(direct);
    }

    private static int NextYear(int year) => year + 1;

    public sealed class Result
    {
        public int Value { get; set; }
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public DateTime? Date { get; set; }
    }

    public sealed class RowContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Row>().Property(row => row.Id).ValueGeneratedNever();
    }
}
