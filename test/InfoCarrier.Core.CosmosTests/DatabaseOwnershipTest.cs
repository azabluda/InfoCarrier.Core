// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class DatabaseOwnershipTest
{
    [Fact]
    public async Task Separate_document_fixture_instances_use_different_databases()
    {
        await using var first = new DocumentStoreFixture();
        await using var second = new DocumentStoreFixture();
        await using var firstContext = first.CreateBackend();
        await using var secondContext = second.CreateBackend();
        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
        Assert.Equal(first.DatabaseName, firstContext.Database.GetCosmosDatabaseId());
        Assert.Equal(second.DatabaseName, secondContext.Database.GetCosmosDatabaseId());
    }

    [Fact]
    public async Task Equal_logical_store_names_do_not_share_backend_databases()
    {
        var factory = InfoCarrierTestStoreFactory.Create(new CosmosTier(), typeof(ShopServerContext), null);
        await using var first = (InfoCarrierTestStore)factory.GetOrCreate("Northwind");
        await using var second = (InfoCarrierTestStore)factory.GetOrCreate("Northwind");
        await using var firstContext = first.Backend.CreateDbContext();
        await using var secondContext = second.Backend.CreateDbContext();
        Assert.Equal(first.Name, second.Name);
        Assert.NotEqual(firstContext.Database.GetCosmosDatabaseId(), secondContext.Database.GetCosmosDatabaseId());
        Assert.NotSame(first.Backend.ServiceProvider, second.Backend.ServiceProvider);
    }
}
