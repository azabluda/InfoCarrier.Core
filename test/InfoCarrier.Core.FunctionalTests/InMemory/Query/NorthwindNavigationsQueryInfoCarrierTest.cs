// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindNavigationsQueryInfoCarrierTest :
        NorthwindNavigationsQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindNavigationsQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Where_subquery_on_navigation_client_eval(bool async)
        {
            return base.Where_subquery_on_navigation_client_eval(async);
        }
    }
}
