// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class NorthwindCompiledQueryInfoCarrierTest :
        NorthwindCompiledQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public NorthwindCompiledQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "See issue #17386")]
        public override void Query_with_array_parameter()
        {
        }

        [ConditionalFact(Skip = "See issue #17386")]
        public override Task Query_with_array_parameter_async()
            => null;

        // Skipped: ParameterExpression type mismatch after Remote.Linq round-trip.
        // SubstituteParametersExpressionVisitor uses ValueWrapper<object> when parameter
        // value is null, causing 'variable referenced from scope but not defined' error
        // during server-side Expression.Compile().
        [ConditionalFact(Skip = "InfoCarrier#ParameterTypeMismatch: null parameter handling in SubstituteParametersExpressionVisitor causes type mismatch. See MIGRATION_STATUS.md Cat 3")]
        public override void Compiled_query_when_does_not_end_in_query_operator()
        {
            base.Compiled_query_when_does_not_end_in_query_operator();
        }
    }
}
