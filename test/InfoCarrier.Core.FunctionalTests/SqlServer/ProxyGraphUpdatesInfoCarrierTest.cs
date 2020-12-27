// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public abstract class ProxyGraphUpdatesInfoCarrierTest
    {
        public abstract class ProxyGraphUpdatesInfoCarrierTestBase<TFixture> : ProxyGraphUpdatesTestBase<TFixture>
            where TFixture : ProxyGraphUpdatesInfoCarrierTestBase<TFixture>.ProxyGraphUpdatesInfoCarrierFixtureBase, new()
        {
            protected ProxyGraphUpdatesInfoCarrierTestBase(TFixture fixture)
                : base(fixture)
            {
            }

            public abstract class ProxyGraphUpdatesInfoCarrierFixtureBase : ProxyGraphUpdatesFixtureBase
            {
                private ITestStoreFactory testStoreFactory;

                protected override ITestStoreFactory TestStoreFactory =>
                    InfoCarrierTestStoreFactory.EnsureInitialized(
                        ref this.testStoreFactory,
                        InfoCarrierTestStoreFactory.SqlServer,
                        this.ContextType,
                        this.OnModelCreating);
            }
        }

        public class LazyLoading : ProxyGraphUpdatesInfoCarrierTestBase<LazyLoading.TestFixture>
        {
            public LazyLoading(TestFixture fixture)
                : base(fixture)
            {
            }

            public class TestFixture : ProxyGraphUpdatesInfoCarrierFixtureBase
            {
                protected override string StoreName { get; } = "ProxyGraphLazyLoadingUpdatesTest";

                public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                    => base.AddOptions(builder.UseLazyLoadingProxies());

                protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
                    => base.AddServices(serviceCollection.AddEntityFrameworkProxies());

                protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
                {
                    modelBuilder.UseIdentityColumns();

                    base.OnModelCreating(modelBuilder, context);
                }
            }
        }
    }
}
