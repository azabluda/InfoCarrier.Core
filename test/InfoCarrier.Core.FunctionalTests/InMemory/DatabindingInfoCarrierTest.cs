// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.InMemory.Metadata.Conventions;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class DatabindingInfoCarrierTest : DatabindingTestBase<DatabindingInfoCarrierTest.TestFixture>
    {
        public DatabindingInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : F1InfoCarrierFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    b => b.UseModel(this.CreateModelExternal()));

            public override ModelBuilder CreateModelBuilder() =>
                new ModelBuilder(InMemoryConventionSetBuilder.Build());
        }
    }
}
