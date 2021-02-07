// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindGroupByQueryInfoCarrierTest :
        NorthwindGroupByQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17536")]
        public override Task Join_GroupBy_Aggregate_with_left_join(bool async)
        {
            return base.Join_GroupBy_Aggregate_with_left_join(async);
        }
    }
}
