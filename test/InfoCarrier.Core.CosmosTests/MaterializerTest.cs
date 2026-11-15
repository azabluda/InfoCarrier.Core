// Licensed under the MIT license. See license.txt file in the project root for license information.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class MaterializerTest(NonSharedFixture fixture) : NonSharedModelTestBase(fixture), IClassFixture<NonSharedFixture>
{
    protected override string NonSharedStoreName { get; } = nameof(MaterializerTest);

    // Official EF Core 11 CosmosMaterializerTest at 9e685c7a52d1a344d133a3d370e458db97417b3f.
    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());
    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }

    #region Shadow key materialization

    [Fact]
    public async Task Materialize_entity_with_shadow_key()
    {
        var factory = await InitializeNonSharedTest<ShadowKeyContext>();

        using (var context = factory.CreateDbContext())
        {
            var entry = context.Entry(new ShadowKeyEntity { Id = 1 });
            entry.Property<string>("Name").CurrentValue = "Name";
            entry.Property<int>("Id2").CurrentValue = 1;
            entry.State = EntityState.Added;
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entity = await context.Entities.SingleAsync(TestContext.Current.CancellationToken);
            var entry = context.Entry(entity);
            Assert.Equal("Name", entry.Property<string>("Name").CurrentValue);
            Assert.Equal(1, entry.Property<int>("Id2").CurrentValue);
        }
    }

    public class ShadowKeyEntity
    {
        public int Id { get; set; }
    }

    public class ShadowKeyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<ShadowKeyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ShadowKeyEntity>(e =>
            {
                e.Property<string>("Name");
                e.Property<int>("Id2");
                e.HasKey("Id", "Id2");
                e.HasPartitionKey(x => x.Id);
            });
        }
    }

    #endregion

    #region Discriminator

    [Fact]
    public async Task Materialize_entity_with_discriminator()
    {
        var factory = await InitializeNonSharedTest<DiscriminatorContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new DiscriminatorDerivedEntity { Name = "Name" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entity = await context.Entities.SingleAsync(TestContext.Current.CancellationToken);
            var derivedEntity = Assert.IsType<DiscriminatorDerivedEntity>(entity);
            Assert.Equal("Name", derivedEntity.Name);
        }
    }

    public abstract class DiscriminatorBaseEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public class DiscriminatorDerivedEntity : DiscriminatorBaseEntity
    {
        public string? Name { get; set; }
    }

    public class DiscriminatorContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<DiscriminatorBaseEntity> Entities
            => Set<DiscriminatorBaseEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DiscriminatorBaseEntity>().HasPartitionKey(x => x.Id);
            modelBuilder.Entity<DiscriminatorDerivedEntity>();
        }
    }

    #endregion

    #region Collection navigation

    [Fact]
    public async Task Materialize_entity_with_collection()
    {
        var factory = await InitializeNonSharedTest<CollectionContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new CollectionEntity() { Entities = [new(), new()] });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entity = await context.Entities.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, entity.Entities.Count);
            foreach (var item in entity.Entities)
            {
                Assert.Equal("Name", item.Name);
            }
        }
    }

    [Fact]
    public async Task Materialize_entity_with_empty_collection()
    {
        var factory = await InitializeNonSharedTest<CollectionContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new CollectionEntity() { Entities = [] });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entity = await context.Entities.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Empty(entity.Entities);
        }
    }

    [Fact]
    public async Task Materialize_entity_with_collection_with_ordinal_key()
    {
        var factory = await InitializeNonSharedTest<CollectionContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new CollectionEntityWithOrdinalKey() { Entities = [new(), new()] });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entity = await context.EntitiesWithOrdinalKey.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, entity.Entities.Count);
            foreach (var item in entity.Entities)
            {
                Assert.Equal("Name", item.Name);
            }
        }
    }

    public class CollectionEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public List<CollectionItemEntity> Entities { get; set; } = [];
    }

    public class CollectionItemEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Name";
    }

    public class CollectionEntityWithOrdinalKey
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public List<CollectionItemEntityWithOrdinalKey> Entities { get; set; } = [];
    }

    public class CollectionItemEntityWithOrdinalKey
    {
        public string Name { get; set; } = "Name";
    }

    public class CollectionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<CollectionEntity> Entities
            => Set<CollectionEntity>();

        public DbSet<CollectionEntityWithOrdinalKey> EntitiesWithOrdinalKey
            => Set<CollectionEntityWithOrdinalKey>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CollectionEntity>().HasPartitionKey(x => x.Id);
            modelBuilder.Entity<CollectionEntity>().OwnsMany(x => x.Entities);

            modelBuilder.Entity<CollectionEntityWithOrdinalKey>().HasPartitionKey(x => x.Id);
            modelBuilder.Entity<CollectionEntityWithOrdinalKey>().OwnsMany(x => x.Entities);
        }
    }

    #endregion

    #region PrimaryKeyWithValueConverter

    [Fact]
    public async Task Materialize_entity_with_value_converter_primary_key()
    {
        var factory = await InitializeNonSharedTest<PrimaryKeyValueConvertedContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new PrimaryKeyValueConvertedEntity { Id = 1 });
            context.Add(new PrimaryKeyValueConvertedEntity { Id = 2 });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var entities = (await context.Entities.ToListAsync(TestContext.Current.CancellationToken)).OrderBy(x => x.Id).ToList();
            Assert.Equal(2, entities.Count);
            Assert.Equal(1, entities[0].Id);
            Assert.Equal(2, entities[1].Id);
        }
    }

    public class PrimaryKeyValueConvertedEntity
    {
        public int Id { get; set; }
    }

    public class PrimaryKeyValueConvertedContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<PrimaryKeyValueConvertedEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<PrimaryKeyValueConvertedEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasPartitionKey(x => x.Id);
                e.Property(x => x.Id).HasConversion(x => x.ToString(), x => int.Parse(x));
            });
        }
    }

    #endregion

    #region UndefinedNestedProperty

    [Fact]
    public async Task Materialize_all_undefined_anon_projection_throws()
    {
        // See: https://github.com/dotnet/efcore/issues/38298#issuecomment-4726236589
        var factory = await InitializeNonSharedTest<UndefinedNestedPropertyContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new UndefinedNestedPropertyEntity());
            context.Add(new UndefinedNestedPropertyEntity { Nested = new UndefinedNestedPropertyNestedEntity { Name = "Name" } });
            context.Add(new UndefinedNestedPropertyEntity { Nested = new UndefinedNestedPropertyNestedEntity() });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            // Coalesce can be used to avoid the exception when the nested property is null
            var results = await context.Entities.Select(x => new { Name = x.Nested!.Name ?? "", Name2 = (x.Nested!.Name ?? "") + "2" })
                .ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, results.Count);
            Assert.Equivalent(new[] { "", "Name", "" }.OrderBy(x => x), results.Select(x => x.Name).OrderBy(x => x));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
                => context.Entities.Select(x => new { x.Nested!.Name, Name2 = x.Nested!.Name + "2" }).ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("A part of the projection was undefined, use the coalesce operator to handle possible undefined values.", ex.Message);
        }
    }

    [Fact]
    public async Task Materialize_last_undefined_anon_projection_throws()
    {
        var factory = await InitializeNonSharedTest<UndefinedNestedPropertyContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new UndefinedNestedPropertyEntity());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
                => context.Entities.Select(x => new { x.AlwaysHere, x.Nested!.Name }).ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("A part of the projection was undefined, use the coalesce operator to handle possible undefined values.", ex.Message);
        }
    }

    [Fact]
    public async Task Materialize_first_undefined_anon_projection_throws()
    {
        var factory = await InitializeNonSharedTest<UndefinedNestedPropertyContext>();

        using (var context = factory.CreateDbContext())
        {
            context.Add(new UndefinedNestedPropertyEntity());
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using (var context = factory.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(()
                => context.Entities.Select(x => new { x.Nested!.Name, x.AlwaysHere }).ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal("A part of the projection was undefined, use the coalesce operator to handle possible undefined values.", ex.Message);
        }
    }

    public class UndefinedNestedPropertyEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string AlwaysHere { get; set; } = "AlwaysHere";
        public UndefinedNestedPropertyNestedEntity? Nested { get; set; }
    }

    public class UndefinedNestedPropertyNestedEntity
    {
        public string? Name { get; set; }
    }

    public class UndefinedNestedPropertyContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UndefinedNestedPropertyEntity> Entities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UndefinedNestedPropertyEntity>(e =>
            {
                e.HasPartitionKey(x => x.Id);
                e.ComplexProperty(x => x.Nested, b => b.IsRequired(false));
            });
        }
    }

    #endregion
}
