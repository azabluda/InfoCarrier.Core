﻿// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit.Abstractions;

    public class AsyncGearsOfWarQueryInfoCarrierTest : AsyncGearsOfWarQueryTestBase<GearsOfWarQueryInfoCarrierFixture>
    {
        public AsyncGearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture testFixture, ITestOutputHelper testOutputHelper)
            : base(testFixture)
        {
        }
    }
}
