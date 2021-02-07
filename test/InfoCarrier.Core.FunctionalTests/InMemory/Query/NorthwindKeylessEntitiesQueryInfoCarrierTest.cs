// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindKeylessEntitiesQueryInfoCarrierTest :
        NorthwindKeylessEntitiesQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindKeylessEntitiesQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        // mapping to view not supported on InMemory
        public override void KeylessEntity_by_database_view()
        {
        }

        public override void Entity_mapped_to_view_on_right_side_of_join()
        {
        }

        public override async Task KeylessEntity_with_included_nav(bool async)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => base.KeylessEntity_with_included_nav(async));
        }
    }
}
