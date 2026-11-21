// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class ConcurrencyFixtureIsolationTest
{
    [Fact]
    public async Task Disposing_one_case_leaves_another_cases_database_and_seed_intact()
    {
        await using var survivor = new CosmosConcurrencyFixture();
        await survivor.InitializeAsync();
        string firstDatabase = Database(survivor);

        await using (var other = new CosmosConcurrencyFixture())
        {
            await other.InitializeAsync();
            Assert.NotEqual(firstDatabase, Database(other));
        }

        using (var client = survivor.CreateContext())
            Assert.NotEmpty(await client.EngineSuppliers.ToListAsync(TestContext.Current.CancellationToken));

        await survivor.ReseedAsync();
        Assert.NotEqual(firstDatabase, Database(survivor));
        using (var client = survivor.CreateContext())
            Assert.NotEmpty(await client.EngineSuppliers.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static string Database(CosmosConcurrencyFixture fixture)
    {
        using var backend = ((IInfoCarrierClientTestStore)fixture.TestStore).Backend.CreateDbContext();
        return backend.Database.GetCosmosDatabaseId();
    }
}
