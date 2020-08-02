// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class IncludeInfoCarrierTest : IncludeTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public IncludeInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [InlineData(false)]
        [InlineData(true)]
        public override void Include_collection_with_last_no_orderby(bool useString)
        {
            base.Include_collection_with_last_no_orderby(useString);
        }
    }
}
