// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierTestHelpers : TestHelpers
    {
        protected InfoCarrierTestHelpers()
        {
        }

        public static InfoCarrierTestHelpers Instance { get; } = new InfoCarrierTestHelpers();

        public override IServiceCollection AddProviderServices(IServiceCollection services)
            => services.AddEntityFrameworkInfoCarrierBackend();

        protected override void UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInfoCarrierBackend(
                new TestInfoCarrierBackend(
                    new SqlServerTestStore(
                        "DummyDatabase",
                        false,
                        new SharedTestStoreProperties
                        {
                            ContextType = typeof(DbContext),
                            OnModelCreating = (b, c) => { },
                        })));

        public DbContextOptionsBuilder CreateOptionsBuilder()
        {
            var builder = new DbContextOptionsBuilder();
            this.UseProviderOptions(builder);
            return builder;
        }
    }
}
