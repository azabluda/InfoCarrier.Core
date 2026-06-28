// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindSelectQueryInfoCarrierTest :
        NorthwindSelectQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindSelectQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Select_bool_closure_with_order_by_property_with_cast_to_nullable(bool async)
        {
            return base.Select_bool_closure_with_order_by_property_with_cast_to_nullable(async);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Projection_when_arithmetic_mixed_subqueries(bool async)
        {
            return base.Projection_when_arithmetic_mixed_subqueries(async);
        }

        [ConditionalTheory(Skip = "Issue#17536")]
        public override Task SelectMany_correlated_with_outer_3(bool async)
        {
            return base.SelectMany_correlated_with_outer_3(async);
        }

        [ConditionalTheory(Skip = "Issue#17386")]
        public override Task Reverse_without_explicit_ordering_throws(bool async)
        {
            return base.Reverse_without_explicit_ordering_throws(async);
        }

        // Skipped: Client method calls in projections require materialization
        // that is not fully supported through Remote.Linq pipeline.
        // See MIGRATION_STATUS.md Cat 6.
        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Client method in projection not supported. See MIGRATION_STATUS.md Cat 6")]
        public override Task Client_method_in_projection_requiring_materialization_1(bool async)
            => base.Client_method_in_projection_requiring_materialization_1(async);

        [ConditionalTheory(Skip = "InfoCarrier#ExpressionTranslation: Client method in projection not supported. See MIGRATION_STATUS.md Cat 6")]
        public override Task Client_method_in_projection_requiring_materialization_2(bool async)
            => base.Client_method_in_projection_requiring_materialization_2(async);
    }
}
