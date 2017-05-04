namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
    using Microsoft.EntityFrameworkCore.Storage.Internal;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.Extensions.DependencyInjection;
    using Server;

    public class InMemoryTestStore<TDbContext> : TestStoreImplBase<TDbContext>
        where TDbContext : DbContext
    {
        private InMemoryTestStore(
            Func<DbContextOptions, TDbContext> createContext,
            Action<IServiceCollection> configureStoreService,
            Action<TDbContext> initializeDatabase,
            Action<WarningsConfigurationBuilder> configureWarnings = null)
            : base(createContext, initializeDatabase)
        {
            var serviceCollection = new ServiceCollection().AddEntityFrameworkInMemoryDatabase();
            configureStoreService(serviceCollection);

            this.DbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase()
                .ConfigureWarnings(configureWarnings ?? (_ => { }))
                .UseInternalServiceProvider(serviceCollection.BuildServiceProvider())
                .Options;
        }

        protected override DbContextOptions DbContextOptions { get; }

        public override TestStoreBase FromShared()
            => new Decorator(this);

        protected override SaveChangesHelper CreateSaveChangesHelper(IEnumerable<IUpdateEntry> entries)
        {
            SaveChangesHelper helper = base.CreateSaveChangesHelper(entries);

            // Temporary values for Key properties generated on the client side should
            // be treated a permanent if the backend database is InMemory
            var tempKeyProps =
                helper.Entries.SelectMany(e =>
                    e.ToEntityEntry().Properties
                        .Where(p => p.IsTemporary && p.Metadata.IsKey())).ToList();

            tempKeyProps.ForEach(p => p.IsTemporary = false);

            return helper;
        }

        public override void Dispose()
        {
            this.DbContextOptions
                .GetExtension<CoreOptionsExtension>()
                .InternalServiceProvider
                .GetRequiredService<IInMemoryStoreSource>()
                .GetGlobalStore()
                .Clear();

            base.Dispose();
        }

        public static InfoCarrierTestHelper<TDbContext> CreateHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, TDbContext> createContext,
            Action<TDbContext> initializeDatabase,
            bool useSharedStore = true,
            Action<WarningsConfigurationBuilder> configureWarnings = null)
        {
            return CreateTestHelper(
                onModelCreating,
                () => new InMemoryTestStore<TDbContext>(
                    createContext,
                    MakeStoreServiceConfigurator<InMemoryModelSource>(onModelCreating, p => new TestInMemoryModelSource(p)),
                    initializeDatabase,
                    configureWarnings),
                useSharedStore);
        }

        private class TestInMemoryModelSource : InMemoryModelSource
        {
            private readonly TestModelSourceParams testModelSourceParams;

            public TestInMemoryModelSource(TestModelSourceParams p)
                : base(p.SetFinder, p.CoreConventionSetBuilder, p.ModelCustomizer, p.ModelCacheKeyFactory)
            {
                this.testModelSourceParams = p;
            }

            public override IModel GetModel(DbContext context, IConventionSetBuilder conventionSetBuilder, IModelValidator validator)
                => this.testModelSourceParams.GetModel(context, conventionSetBuilder, validator);
        }
    }
}