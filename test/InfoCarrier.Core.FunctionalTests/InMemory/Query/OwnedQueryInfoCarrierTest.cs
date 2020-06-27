// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System.Linq;
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;
    using Xunit.Abstractions;

    public class OwnedQueryInfoCarrierTest : OwnedQueryTestBase<OwnedQueryInfoCarrierTest.TestFixture>
    {
        public OwnedQueryInfoCarrierTest(TestFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        [ConditionalTheory(Skip = "Need to transfer all tracked entities from server to client.")]
        [MemberData(nameof(IsAsyncData))]
        public override Task Unmapped_property_projection_loads_owned_navigations(bool isAsync)
        {
            return base.Unmapped_property_projection_loads_owned_navigations(isAsync);
        }

        public class TestFixture : OwnedQueryFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating);
        }
    }
}
