// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public class LoggingInfoCarrierTest : LoggingTestBase
    {
        protected override string ProviderName => @"InfoCarrier.Core";

        protected override string DefaultOptions => @"InfoCarrierServerUrl=DummyDatabase ";

        protected override DbContextOptionsBuilder CreateOptionsBuilder(IServiceCollection services)
            => new DbContextOptionsBuilder()
                .UseInfoCarrierClient(InfoCarrierTestHelpers.CreateDummyClient(typeof(DbContext)))
                .UseInternalServiceProvider(services.AddEntityFrameworkInfoCarrierClient().BuildServiceProvider());
    }
}
