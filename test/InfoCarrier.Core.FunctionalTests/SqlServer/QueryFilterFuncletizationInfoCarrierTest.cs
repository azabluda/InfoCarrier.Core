// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.SqlServer
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
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
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.SqlServer,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.EnableSensitiveDataLogging(),
                    (c1, c2) => CopyDbContextParameters((QueryFilterFuncletizationContext)c1, (QueryFilterFuncletizationContext)c2));

            private static void CopyDbContextParameters(QueryFilterFuncletizationContext clientDbContext, QueryFilterFuncletizationContext backendDbContext)
            {
                backendDbContext.Field = clientDbContext.Field;
                backendDbContext.Property = clientDbContext.Property;
                backendDbContext.Tenant = clientDbContext.Tenant;
                backendDbContext.TenantIds = clientDbContext.TenantIds;
                backendDbContext.IndirectionFlag = clientDbContext.IndirectionFlag;
                backendDbContext.IsModerated = clientDbContext.IsModerated;
            }
        }
    }
}
