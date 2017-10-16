// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;

    public class UpdatesInfoCarrierTest : UpdatesTestBase<UpdatesInfoCarrierFixture, TestStoreBase>
    {
        public UpdatesInfoCarrierTest(UpdatesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        protected override string UpdateConcurrencyMessage
            => InMemoryStrings.UpdateConcurrencyException;
    }
}