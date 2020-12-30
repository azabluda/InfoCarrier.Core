// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class WithConstructorsInfoCarrierTest : WithConstructorsTestBase<WithConstructorsInfoCarrierTest.TestFixture>
    {
        public WithConstructorsInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalFact]
        public override void Query_and_update_using_constructors_with_property_parameters()
        {
            base.Query_and_update_using_constructors_with_property_parameters();

            this.Fixture.Reseed();
        }

        [ConditionalFact]
        public override void Query_with_loader_injected_into_property_via_constructor_for_reference()
        {
            using (var context = this.CreateContext())
            {
                var post = context.Set<LazyPcPost>().OrderBy(e => e.Id).First();

                // Assert on LoaderSetterCalled is insignificant. InfoCarrier.Core is setting service properties.
                // Assert.False(post.LoaderSetterCalled);
                Assert.NotNull(post.LazyPcBlog);
                Assert.Contains(post, post.LazyPcBlog.LazyPcPosts);
            }
        }

        [ConditionalFact]
        public override void Query_with_loader_injected_into_property_via_constructor_for_collections()
        {
            using (var context = this.CreateContext())
            {
                var blog = context.Set<LazyPcBlog>().Single();

                // Assert on LoaderSetterCalled is insignificant. InfoCarrier.Core is setting service properties.
                // Assert.False(blog.LoaderSetterCalled);
                Assert.Equal(2, blog.LazyPcPosts.Count());
                Assert.Same(blog, blog.LazyPcPosts.First().LazyPcBlog);
                Assert.Same(blog, blog.LazyPcPosts.Skip(1).First().LazyPcBlog);
            }
        }

        [ConditionalFact]
        public override void Query_with_loader_delegate_injected_into_property_via_constructor_for_reference()
        {
            using (var context = this.CreateContext())
            {
                var post = context.Set<LazyPcsPost>().OrderBy(e => e.Id).First();

                // Assert on LoaderSetterCalled is insignificant. InfoCarrier.Core is setting service properties.
                // Assert.False(post.LoaderSetterCalled);
                Assert.NotNull(post.LazyPcsBlog);
                Assert.Contains(post, post.LazyPcsBlog.LazyPcsPosts);
            }
        }

        [ConditionalFact]
        public override void Query_with_loader_delegate_injected_into_property_via_constructor_for_collections()
        {
            using (var context = this.CreateContext())
            {
                var blog = context.Set<LazyPcsBlog>().Single();

                // Assert on LoaderSetterCalled is insignificant. InfoCarrier.Core is setting service properties.
                // Assert.False(blog.LoaderSetterCalled);
                Assert.Equal(2, blog.LazyPcsPosts.Count());
                Assert.Same(blog, blog.LazyPcsPosts.First().LazyPcsBlog);
                Assert.Same(blog, blog.LazyPcsPosts.Skip(1).First().LazyPcsBlog);
            }
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
                    .Entity<BlogQuery>()
                    .HasNoKey()
                    .ToQuery(() => context.Set<Blog>().Select(b => new BlogQuery(b.Title, b.MonthlyRevenue)));
            }
        }
    }
}
