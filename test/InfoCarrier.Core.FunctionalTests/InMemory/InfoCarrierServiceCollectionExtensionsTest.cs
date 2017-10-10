// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierServiceCollectionExtensionsTest : EntityFrameworkServiceCollectionExtensionsTest
    {
        public InfoCarrierServiceCollectionExtensionsTest()
            : base(new InfoCarrierTestHelpers())
        {
        }

        private class InfoCarrierTestHelpers : TestHelpers
        {
            private readonly IInfoCarrierBackend backend = new SimpleInMemoryTestStore(nameof(InfoCarrierServiceCollectionExtensionsTest));

            public override IServiceCollection AddProviderServices(IServiceCollection services)
                => services.AddEntityFrameworkInfoCarrierBackend();

            protected override void UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseInfoCarrierBackend(this.backend);
        }
    }
}
