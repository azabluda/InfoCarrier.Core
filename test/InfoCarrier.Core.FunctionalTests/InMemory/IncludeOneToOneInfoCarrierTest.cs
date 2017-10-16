// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;

    public class IncludeOneToOneInfoCarrierTest : IncludeOneToOneTestBase, IClassFixture<OneToOneQueryInfoCarrierFixture>
    {
        private readonly OneToOneQueryInfoCarrierFixture fixture;

        public IncludeOneToOneInfoCarrierTest(OneToOneQueryInfoCarrierFixture fixture)
        {
            this.fixture = fixture;
        }

        protected override DbContext CreateContext() => this.fixture.CreateContext();
    }
}
