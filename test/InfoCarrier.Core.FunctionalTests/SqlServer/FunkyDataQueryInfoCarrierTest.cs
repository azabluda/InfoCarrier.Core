// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.FunkyDataModel;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit.Abstractions;

    public class FunkyDataQueryInfoCarrierTest : FunkyDataQueryTestBase<FunkyDataQueryInfoCarrierTest.TestFixture>
    {
        public FunkyDataQueryInfoCarrierTest(TestFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        public class TestFixture : FunkyDataQueryFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.SqlServer,
                    this.ContextType,
                    this.OnModelCreating);

            public override FunkyDataContext CreateContext()
            {
                var context = base.CreateContext();
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return context;
            }
        }
    }
}
