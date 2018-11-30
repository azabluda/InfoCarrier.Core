// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.InMemory.Internal;
    using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;

    public abstract class UpdatesInfoCarrierTestBase<TFixture> : UpdatesTestBase<TFixture>
        where TFixture : UpdatesInfoCarrierFixtureBase
    {
        protected UpdatesInfoCarrierTestBase(TFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyMessage
            => InMemoryStrings.UpdateConcurrencyException;

        protected override void ExecuteWithStrategyInTransaction(
            Action<UpdatesContext> testOperation,
            Action<UpdatesContext> nestedTestOperation1 = null,
            Action<UpdatesContext> nestedTestOperation2 = null)
        {
            base.ExecuteWithStrategyInTransaction(testOperation, nestedTestOperation1, nestedTestOperation2);
            this.Fixture.Reseed();
        }

        protected override async Task ExecuteWithStrategyInTransactionAsync(
            Func<UpdatesContext, Task> testOperation,
            Func<UpdatesContext, Task> nestedTestOperation1 = null,
            Func<UpdatesContext, Task> nestedTestOperation2 = null)
        {
            await base.ExecuteWithStrategyInTransactionAsync(testOperation, nestedTestOperation1, nestedTestOperation2);
            this.Fixture.Reseed();
        }
    }
}
