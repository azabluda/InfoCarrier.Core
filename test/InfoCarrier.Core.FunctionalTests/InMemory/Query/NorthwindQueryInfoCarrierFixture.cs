// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Northwind;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class NorthwindQueryInfoCarrierFixture<TModelCustomizer> : NorthwindQueryFixtureBase<TModelCustomizer>
        where TModelCustomizer : IModelCustomizer, new()
    {
        private ITestStoreFactory testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory =>
            InfoCarrierTestStoreFactory.EnsureInitialized(
                ref this.testStoreFactory,
                InfoCarrierTestStoreFactory.InMemory,
                this.ContextType,
                this.OnModelCreating,
                copyDbContextParameters: (c1, c2) => CopyDbContextParameters((NorthwindContext)c1, (NorthwindContext)c2));

        private static void CopyDbContextParameters(NorthwindContext clientDbContext, NorthwindContext backendDbContext)
        {
            backendDbContext.TenantPrefix = clientDbContext.TenantPrefix;
        }
    }
}
