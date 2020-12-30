// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class QueryNavigationsInfoCarrierTest : QueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public QueryNavigationsInfoCarrierTest(
            NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Where_subquery_on_navigation_client_eval(bool isAsync)
        {
            return base.Where_subquery_on_navigation_client_eval(isAsync);
        }
    }
}
