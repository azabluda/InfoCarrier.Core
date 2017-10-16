// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore.Query;

    public class IncludeAsyncInfoCarrierTest : IncludeAsyncTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public IncludeAsyncInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
