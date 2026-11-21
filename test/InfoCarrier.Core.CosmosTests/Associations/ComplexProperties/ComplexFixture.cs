// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.ComplexProperties;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.CosmosTests.Associations.ComplexProperties;

// Match the pinned EF Core 11 ComplexPropertiesCosmosFixture mapping on both sides of the wire.
public class ComplexFixture : ComplexPropertiesFixtureBase
{
    private ITestStoreFactory? _factory;
    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), typeof(PoolableDbContext), OnModelCreating,
        onAddOptions: options => options.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.MappedEntityTypeIgnoredWarning)),
        configureConventions: ConfigureConventions,
        configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder).ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.MappedEntityTypeIgnoredWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        modelBuilder.Ignore<RootReferencingEntity>();
        modelBuilder.Entity<RootEntity>().ToContainer("RootEntities");
        modelBuilder.Entity<ValueRootEntity>().ToContainer("ValueRootEntities");
    }
}
