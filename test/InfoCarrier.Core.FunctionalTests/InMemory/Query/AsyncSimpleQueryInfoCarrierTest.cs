// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class AsyncSimpleQueryInfoCarrierTest : AsyncSimpleQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsyncSimpleQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "Concurrency detection mechanism cannot be used")]
        public override Task Throws_on_concurrent_query_list()
        {
            return base.Throws_on_concurrent_query_list();
        }

        [ConditionalFact(Skip = "Concurrency detection mechanism cannot be used")]
        public override Task Throws_on_concurrent_query_first()
        {
            return base.Throws_on_concurrent_query_first();
        }
    }
}
