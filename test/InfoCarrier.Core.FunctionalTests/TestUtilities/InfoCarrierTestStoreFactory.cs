// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierTestStoreFactory : ITestStoreFactory
    {
        private readonly SharedTestStoreProperties testStoreProperties;
        private readonly InfoCarrierBackendTestStoreFactory backendTestStoreFactory;

        public InfoCarrierTestStoreFactory(SharedTestStoreProperties testStoreProperties, InfoCarrierBackendTestStoreFactory backendTestStoreFactory)
        {
            this.testStoreProperties = testStoreProperties;
            this.backendTestStoreFactory = backendTestStoreFactory;
        }

        public delegate InfoCarrierBackendTestStore InfoCarrierBackendTestStoreFactory(string name, bool shared, SharedTestStoreProperties testStoreProperties);

        public static InfoCarrierBackendTestStoreFactory InMemory => (name, shared, props) => new InMemoryTestStore(name, shared, props);

        public static InfoCarrierBackendTestStoreFactory SqlServer => (name, shared, props) => new SqlServerTestStore(name, shared, props);

        public static ITestStoreFactory EnsureInitialized(
            ref ITestStoreFactory inst,
            InfoCarrierBackendTestStoreFactory backendTestStoreFactory,
            Type contextType,
            Action<ModelBuilder, DbContext> onModelCreating,
            Func<DbContextOptionsBuilder, DbContextOptionsBuilder> onAddOptions = null)
            => NonCapturingLazyInitializer.EnsureInitialized(
                ref inst,
                inst,
                _ => new InfoCarrierTestStoreFactory(
                   new SharedTestStoreProperties
                   {
                       ContextType = contextType,
                       OnModelCreating = onModelCreating,
                       OnAddOptions = onAddOptions ?? (o => o),
                   },
                   backendTestStoreFactory));

        public virtual TestStore Create(string storeName)
            => new InfoCarrierTestStore(this.backendTestStoreFactory(storeName, false, this.testStoreProperties));

        public virtual TestStore GetOrCreate(string storeName)
            => new InfoCarrierTestStore(this.backendTestStoreFactory(storeName, true, this.testStoreProperties));

        public IServiceCollection AddProviderServices(IServiceCollection serviceCollection)
            => serviceCollection.AddEntityFrameworkInfoCarrierBackend()
                .AddSingleton(TestModelSource.GetFactory(this.testStoreProperties.OnModelCreating));
    }
}
