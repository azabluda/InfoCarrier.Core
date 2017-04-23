namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;

    public abstract class TestStoreImplBase<TDbContext> : TestStoreImplBase
        where TDbContext : DbContext
    {
        private readonly Func<DbContextOptions, TDbContext> createContext;
        private Action<TDbContext> initializeDatabase;

        protected TestStoreImplBase(
            Func<DbContextOptions, TDbContext> createContext,
            Action<TDbContext> initializeDatabase)
        {
            this.createContext = createContext;
            this.initializeDatabase = initializeDatabase;
        }

        protected sealed override DbContext CreateContextInternal()
            => this.CreateContext();

        public TDbContext CreateContext(DbContextOptions options)
            => this.createContext(options);

        public virtual TDbContext CreateContext()
        {
            this.EnsureInitialized();
            return this.CreateContext(this.DbContextOptions);
        }

        protected void EnsureInitialized()
        {
            if (this.initializeDatabase == null)
            {
                return;
            }

            var init = this.initializeDatabase;
            this.initializeDatabase = null;
            using (TDbContext context = this.CreateContext())
            {
                init(context);
            }
        }

        protected static InfoCarrierTestHelper<TDbContext> CreateTestHelper(
            Action<ModelBuilder> onModelCreating,
            Func<TestStoreImplBase<TDbContext>> createTestStore,
            bool useSharedStore)
        {
            return new InfoCarrierTestHelper<TDbContext>(
                MakeStoreServiceConfigurator<InfoCarrierModelSource>(onModelCreating, p => new TestInfoCarrierModelSource(p)),
                createTestStore,
                useSharedStore);
        }
    }
}
