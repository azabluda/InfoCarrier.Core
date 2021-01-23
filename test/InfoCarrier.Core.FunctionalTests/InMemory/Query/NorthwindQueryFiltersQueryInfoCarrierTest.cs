// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;

    public class NorthwindQueryFiltersQueryInfoCarrierTest :
        NorthwindQueryFiltersQueryTestBase<NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer>>
    {
        public NorthwindQueryFiltersQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NorthwindQueryFiltersCustomizer> fixture)
            : base(fixture)
        {
        }
    }
}
