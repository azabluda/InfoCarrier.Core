// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class AsNoTrackingInfoCarrierTest : AsNoTrackingTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsNoTrackingInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }
    }
}
