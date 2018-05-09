// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
    using Xunit.Abstractions;

    public class AsyncGroupByQueryInfoCarrierTest : AsyncGroupByQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsyncGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "See issue#9591")]
        public override Task Select_Distinct_GroupBy()
        {
            return base.Select_Distinct_GroupBy();
        }
    }
}
