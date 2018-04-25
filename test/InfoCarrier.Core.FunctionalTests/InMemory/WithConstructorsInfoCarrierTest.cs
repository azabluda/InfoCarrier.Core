// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;

    public class WithConstructorsInfoCarrierTest : WithConstructorsTestBase<WithConstructorsInfoCarrierTest.TestFixture>
    {
        public WithConstructorsInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public override void Query_and_update_using_constructors_with_property_parameters()
        {
            base.Query_and_update_using_constructors_with_property_parameters();

            this.Fixture.Reseed();
        }

        public class TestFixture : WithConstructorsFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

                modelBuilder
                    .Query<BlogQuery>()
                    .ToQuery(() => context.Set<Blog>().Select(b => new BlogQuery(b.Title, b.MonthlyRevenue)));
            }
        }
    }
}
