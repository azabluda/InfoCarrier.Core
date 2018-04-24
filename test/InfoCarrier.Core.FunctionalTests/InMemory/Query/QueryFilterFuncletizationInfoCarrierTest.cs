// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;
    using Xunit.Abstractions;

    public class QueryFilterFuncletizationInfoCarrierTest : QueryFilterFuncletizationTestBase<QueryFilterFuncletizationInfoCarrierTest.TestFixture>
    {
        public QueryFilterFuncletizationInfoCarrierTest(TestFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        public class TestFixture : QueryFilterFuncletizationRelationalFixture
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.InMemory);
        }
    }
}
