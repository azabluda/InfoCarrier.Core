// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public abstract class UpdatesInfoCarrierFixtureBase : UpdatesFixtureBase
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
