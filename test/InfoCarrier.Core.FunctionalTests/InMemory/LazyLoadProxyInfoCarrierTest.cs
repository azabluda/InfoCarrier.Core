// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class LazyLoadProxyInfoCarrierTest : LazyLoadProxyTestBase<LazyLoadProxyInfoCarrierTest.TestFixture>
    {
        public LazyLoadProxyInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalFact]
        public override void Top_level_projection_track_entities_before_passing_to_client_method()
        {
            using (var context = this.CreateContext(lazyLoadingEnabled: true))
            {
                var query = (from p in context.Set<Parent>()
                             select p).FirstOrDefault();

                // [ClientEval] Cannot use DtoFactory.CreateDto on the server side
                var dto = DtoFactory.CreateDto(query);

                Assert.NotNull(((dynamic)dto).Single);
            }
        }

        private static class DtoFactory
        {
            public static object CreateDto(Parent parent)
            {
                return new
                {
                    parent.Id,
                    parent.Single,
                    parent.Single.ParentId,
                };
            }
        }

        public class TestFixture : LoadFixtureBase
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
