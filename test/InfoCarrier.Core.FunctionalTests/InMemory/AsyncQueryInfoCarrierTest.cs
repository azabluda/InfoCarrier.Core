// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class AsyncQueryInfoCarrierTest : AsyncQueryTestBase<NorthwindQueryInfoCarrierFixture>
    {
        public AsyncQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact(Skip = "Not valid for in-memory (from AsyncQueryInMemoryTest)")]
        public override Task ToList_on_nav_in_projection_is_async()
        {
            return base.ToList_on_nav_in_projection_is_async();
        }

        [Fact(Skip = "https://github.com/aspnet/EntityFramework/issues/9301")]
        public override Task Mixed_sync_async_query()
        {
            return base.Mixed_sync_async_query();
        }
    }
}
