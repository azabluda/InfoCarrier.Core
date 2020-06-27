// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class CompiledQueryInfoCarrierTest : CompiledQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public CompiledQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "See issue #17386")]
        public override void Query_with_array_parameter()
        {
        }

        [ConditionalFact(Skip = "See issue #17386")]
        public override Task Query_with_array_parameter_async() => null;
    }
}
