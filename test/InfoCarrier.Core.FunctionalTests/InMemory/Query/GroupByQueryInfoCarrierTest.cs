// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
    using Xunit.Abstractions;

    public class GroupByQueryInfoCarrierTest : GroupByQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public GroupByQueryInfoCarrierTest(
            NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture,
            ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "See issue #9591")]
        public override void Select_Distinct_GroupBy()
        {
            base.Select_Distinct_GroupBy();
        }
    }
}
