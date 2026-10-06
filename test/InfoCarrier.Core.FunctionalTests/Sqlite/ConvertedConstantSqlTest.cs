// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Net;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>A constant's inaccessible runtime subtype must not change its SQL conversion.</summary>
public class ConvertedConstantSqlTest
{
    [ConditionalTheory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task An_address_constant_keeps_EFs_conversion(bool async, bool privateSubtype)
    {
        await using SqliteInfoCarrierBackendTestStore store = new(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(AddressContext),
                OnModelCreating = (_, _) => { },
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: async context =>
        {
            context.Add(new AddressRow { Id = 1, Address = IPAddress.Loopback });
            await context.SaveChangesAsync();
        });

        using DbContext server = store.CreateDbContext();
        await using AddressContext client = new(new DbContextOptionsBuilder<AddressContext>()
            .UseInfoCarrier(store).Options);

        async Task<int> Execute(DbContext context)
        {
            IQueryable<AddressRow> query = privateSubtype
                ? context.Set<AddressRow>().Where(row => row.Address == IPAddress.Loopback)
                : context.Set<AddressRow>().Where(row => row.Address == IPAddress.Parse("127.0.0.1"));
            return async ? await query.CountAsync() : query.Count();
        }

        store.ServerSql.Clear();
        Assert.Equal(1, await Execute(server));
        string[] direct = [.. store.ServerSql.Statements];
        if (privateSubtype)
        {
            Assert.Contains("CAST(", Assert.Single(direct), StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("CAST(", Assert.Single(direct), StringComparison.Ordinal);
        }

        store.ServerSql.Clear();
        Assert.Equal(1, await Execute(client));
        store.AssertServerSql(direct);
    }

    public sealed class AddressRow
    {
        public int Id { get; set; }
        public IPAddress Address { get; set; } = IPAddress.None;
    }

    public sealed class AddressContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AddressRow>().Property(row => row.Address)
                .HasConversion(address => address.ToString(), text => IPAddress.Parse(text));
    }
}
