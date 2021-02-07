// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindJoinQueryInfoCarrierTest :
        NorthwindJoinQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindJoinQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#21200")]
        public override Task SelectMany_with_client_eval(bool async)
        {
            return base.SelectMany_with_client_eval(async);
        }

        [ConditionalTheory(Skip = "Issue#21200")]
        public override Task SelectMany_with_client_eval_with_collection_shaper(bool async)
        {
            return base.SelectMany_with_client_eval_with_collection_shaper(async);
        }

        [ConditionalTheory(Skip = "Issue#21200")]
        public override Task SelectMany_with_client_eval_with_collection_shaper_ignored(bool async)
        {
            return base.SelectMany_with_client_eval_with_collection_shaper_ignored(async);
        }

        [ConditionalTheory(Skip = "Issue#21200")]
        public override Task SelectMany_with_client_eval_with_constructor(bool async)
        {
            return base.SelectMany_with_client_eval_with_constructor(async);
        }
    }
}
