// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestModels.BasicTypesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.CosmosTests.Query;

/// <summary>
/// Uses the official Cosmos basic-types mapping with InfoCarrier on the client.
/// Source: EF Core 11, Query/Translations/BasicTypesQueryCosmosFixture.cs.
/// </summary>
public class BasicTypesFixture : BasicTypesQueryFixtureBase
{
    private ITestStoreFactory? _factory;

    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), typeof(BasicTypesContext), OnModelCreating,
        configureConventions: ConfigureConventions,
        configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        modelBuilder.Entity<BasicTypesEntity>(builder =>
        {
            builder.ToContainer(nameof(BasicTypesEntity));
            builder.HasPartitionKey(entity => entity.Id);
        });
        modelBuilder.Entity<NullableBasicTypesEntity>(builder =>
        {
            builder.ToContainer(nameof(NullableBasicTypesEntity));
            builder.HasPartitionKey(entity => entity.Id);
        });
    }
}
