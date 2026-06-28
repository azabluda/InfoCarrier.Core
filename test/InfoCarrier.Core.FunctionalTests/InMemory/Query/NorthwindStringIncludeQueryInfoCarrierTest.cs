// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindStringIncludeQueryInfoCarrierTest :
        NorthwindStringIncludeQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindStringIncludeQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        // Skipped: Include with client method in projection not supported
        // through Remote.Linq pipeline. See MIGRATION_STATUS.md Cat 10.
        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Include with client method not supported. See MIGRATION_STATUS.md Cat 10")]
        public override Task Include_is_not_ignored_when_projection_contains_client_method_and_complex_expression(bool async)
            => base.Include_is_not_ignored_when_projection_contains_client_method_and_complex_expression(async);
    }
}
