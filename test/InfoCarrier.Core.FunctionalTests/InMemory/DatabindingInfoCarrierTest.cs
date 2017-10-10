// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class DatabindingInfoCarrierTest : DatabindingTestBase<TestStoreBase, F1InfoCarrierFixture>
    {
        public DatabindingInfoCarrierTest(F1InfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
