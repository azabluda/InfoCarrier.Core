// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public abstract class TestStoreImplBase<TDbContext> : TestStoreImplBase
        where TDbContext : DbContext
    {
        private readonly Func<DbContextOptions, TDbContext, TDbContext> createContext;
        private Action<TDbContext> initializeDatabase;

        protected TestStoreImplBase(
            Func<DbContextOptions, TDbContext, TDbContext> createContext,
            Action<TDbContext> initializeDatabase)
        {
            this.createContext = createContext;
            this.initializeDatabase = initializeDatabase;
        }

        protected sealed override DbContext CreateStoreContextInternal(DbContext clientDbContext)
            => this.CreateStoreContext(clientDbContext as TDbContext);

        public TDbContext CreateContext(DbContextOptions options)
            => this.createContext(options, null);

        public virtual TDbContext CreateStoreContext(TDbContext clientDbContext)
        {
            this.EnsureInitialized();
            return this.createContext(this.DbContextOptions, clientDbContext);
        }

        protected void EnsureInitialized()
        {
            if (this.initializeDatabase == null)
            {
                return;
            }

            var init = this.initializeDatabase;
            this.initializeDatabase = null;
            using (TDbContext context = this.CreateStoreContext(null))
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
                MakeStoreServiceConfigurator(onModelCreating),
                createTestStore,
                useSharedStore);
        }
    }
}
