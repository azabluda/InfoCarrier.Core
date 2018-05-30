// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using InfoCarrier.Core.FunctionalTests.InMemory.Query;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class ConcurrencyDetectorInfoCarrierTest : ConcurrencyDetectorTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public ConcurrencyDetectorInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }
    }
}
