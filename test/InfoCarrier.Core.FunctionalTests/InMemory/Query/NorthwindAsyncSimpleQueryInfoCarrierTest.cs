// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindAsyncSimpleQueryInfoCarrierTest :
        NorthwindAsyncSimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindAsyncSimpleQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        // InMemory can throw server side exception
        public override Task Average_on_nav_subquery_in_projection()
        {
            return Assert.ThrowsAsync<InvalidOperationException>(() => base.Average_on_nav_subquery_in_projection());
        }

        // mapping to view not supported on InMemory
        public override Task Query_backed_by_database_view()
            => Task.CompletedTask;

        // Skipped: Concurrent query detection is not supported through the Remote.Linq pipeline.
        // Upstream InMemory NorthwindMiscellaneous test also skips this (Issue#17019).
        [Fact(Skip = "Issue#17019: Concurrent query detection not supported through Remote.Linq wire protocol. Upstream InMemory also skips this.")]
        public override Task Throws_on_concurrent_query_list()
            => base.Throws_on_concurrent_query_list();

        [Fact(Skip = "Issue#17019: Concurrent query detection not supported through Remote.Linq wire protocol. Upstream InMemory also skips this.")]
        public override Task Throws_on_concurrent_query_first()
            => base.Throws_on_concurrent_query_first();
    }
}
