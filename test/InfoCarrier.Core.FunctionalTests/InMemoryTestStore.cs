﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using System.Linq;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Storage.Internal;
    using Microsoft.Extensions.DependencyInjection;
    using Server;

    public class InMemoryTestStore<TDbContext> : TestStoreImplBase<TDbContext>
        where TDbContext : DbContext
    {
        private InMemoryTestStore(
            Func<DbContextOptions, TDbContext, TDbContext> createContext,
            Action<IServiceCollection> configureStoreService,
            Action<TDbContext> initializeDatabase,
            Action<WarningsConfigurationBuilder> configureWarnings = null)
            : base(createContext, initializeDatabase)
        {
            var serviceCollection = new ServiceCollection().AddEntityFrameworkInMemoryDatabase();
            configureStoreService(serviceCollection);

            this.DbContextOptions = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(nameof(TDbContext))
                .ConfigureWarnings(configureWarnings ?? (_ => { }))
                .UseInternalServiceProvider(serviceCollection.BuildServiceProvider())
                .Options;
        }

        protected override DbContextOptions DbContextOptions { get; }

        public override string ServerUrl
            => this.DbContextOptions.GetExtension<InMemoryOptionsExtension>().StoreName;

        public override TestStoreBase FromShared()
            => new Decorator(this);

        protected override SaveChangesHelper CreateSaveChangesHelper(SaveChangesRequest request)
        {
            SaveChangesHelper helper = base.CreateSaveChangesHelper(request);

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
                .GetRequiredService<IInMemoryStoreCache>()
                .GetStore(nameof(TDbContext))
                .Clear();

            base.Dispose();
        }

        public static InfoCarrierTestHelper<TDbContext> CreateHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, TDbContext, TDbContext> createContext,
            Action<TDbContext> initializeDatabase,
            bool useSharedStore = true,
            Action<WarningsConfigurationBuilder> configureWarnings = null)
            => CreateTestHelper(
                onModelCreating,
                () => new InMemoryTestStore<TDbContext>(
                    createContext,
                    MakeStoreServiceConfigurator(onModelCreating),
                    initializeDatabase,
                    configureWarnings),
                useSharedStore);

        public static InfoCarrierTestHelper<TDbContext> CreateHelper(
            Action<ModelBuilder> onModelCreating,
            Func<DbContextOptions, TDbContext> createContext,
            Action<TDbContext> initializeDatabase,
            bool useSharedStore = true,
            Action<WarningsConfigurationBuilder> configureWarnings = null)
            => CreateHelper(
                onModelCreating,
                (o, _) => createContext(o),
                initializeDatabase,
                useSharedStore,
                configureWarnings);
    }
}