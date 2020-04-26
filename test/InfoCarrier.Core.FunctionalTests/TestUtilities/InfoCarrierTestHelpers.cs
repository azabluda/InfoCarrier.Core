// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using InfoCarrier.Core.Client;
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
            => services.AddEntityFrameworkInfoCarrierClient();

        protected override void UseProviderOptions(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseInfoCarrierClient(CreateDummyClient(optionsBuilder.Options.ContextType));

        //public static IInfoCarrierClient CreateDummyClient(Type contextType)
        //    => new SqlServerTestStore(
        //        "DummyDatabase",
        //        false,
        //        new SharedTestStoreProperties
        //        {
        //            ContextType = contextType,
        //            OnModelCreating = (b, c) => { },
        //        });
    }
}
