// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using TestUtilities;

    public class LoggingInfoCarrierTest : LoggingTestBase
    {
        protected override string ProviderName => @"InfoCarrier.Core";

        protected override string DefaultOptions => @"InfoCarrierServerUrl=DummyDatabase ";

        protected override DbContextOptionsBuilder CreateOptionsBuilder()
            => InfoCarrierTestHelpers.Instance.CreateOptionsBuilder();
    }
}
