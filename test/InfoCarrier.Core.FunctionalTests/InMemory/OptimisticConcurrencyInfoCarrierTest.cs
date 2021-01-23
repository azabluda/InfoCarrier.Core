// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;

    public class OptimisticConcurrencyInfoCarrierTest : OptimisticConcurrencyTestBase<F1InfoCarrierFixture, byte[]>
    {
        public OptimisticConcurrencyInfoCarrierTest(F1InfoCarrierFixture fixture)
            : base(fixture)
        {
        }
    }
}
