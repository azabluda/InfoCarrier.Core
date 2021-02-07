// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class SpatialQueryInfoCarrierTest : SpatialQueryTestBase<SpatialQueryInfoCarrierTest.TestFixture>
    {
        public SpatialQueryInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "issue #19661")]
        public override Task Distance_constant_lhs(bool async)
        {
            return base.Distance_constant_lhs(async);
        }

        [ConditionalTheory(Skip = "issue #19664")]
        public override Task Intersects_equal_to_null(bool async)
        {
            return base.Intersects_equal_to_null(async);
        }

        [ConditionalTheory(Skip = "issue #19664")]
        public override Task Intersects_not_equal_to_null(bool async)
        {
            return base.Intersects_not_equal_to_null(async);
        }

        public override Task GetGeometryN_with_null_argument(bool async)
        {
            // Sequence contains no elements
            return Task.CompletedTask;
        }

        public class TestFixture : SpatialQueryFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating);
        }
    }
}
