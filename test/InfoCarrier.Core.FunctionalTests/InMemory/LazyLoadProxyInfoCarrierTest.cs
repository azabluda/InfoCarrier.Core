// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Linq;
    using System.Threading.Tasks;
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

        // Skipped: Castle.Core proxy types are [Serializable] but their base types are not,
        // causing FormatterServices.GetSerializableMembers to throw SerializationException.
        // Fix requires configuring DynamicObjectMapper with UtilizeFormatterServices=false
        // in the Remote.Linq expression translation pipeline.
        // See MIGRATION_STATUS.md "Failure Category 1" for details.
        [Theory(Skip = "InfoCarrier#SerializationException: Castle.Core proxy type serialization fails. See MIGRATION_STATUS.md")]
        [InlineData(true)]
        [InlineData(false)]
        public override async Task Entity_equality_with_proxy_parameter(bool async)
        {
            await base.Entity_equality_with_proxy_parameter(async);
        }

        [ConditionalFact]
        public override void Top_level_projection_track_entities_before_passing_to_client_method()
        {
            using var context = this.CreateContext(lazyLoadingEnabled: true);
            var query = (from p in context.Set<Parent>()
                         orderby p.Id
                         select p).FirstOrDefault();

            // [ClientEval] Cannot use DtoFactory.CreateDto on the server side
            var dto = DtoFactory.CreateDto(query);

            Assert.NotNull(((dynamic)dto).Single);
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
