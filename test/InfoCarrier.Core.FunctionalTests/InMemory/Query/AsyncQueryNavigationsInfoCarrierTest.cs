// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit.Abstractions;

    public class AsyncQueryNavigationsInfoCarrierTest : AsyncQueryNavigationsTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>
    {
        public AsyncQueryNavigationsInfoCarrierTest(
            NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }
    }
}
