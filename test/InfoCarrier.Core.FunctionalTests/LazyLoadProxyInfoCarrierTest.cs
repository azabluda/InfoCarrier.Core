// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>LazyLoadProxyTestBase</c> on ADR-009 Tier A, mirroring EF's own
///     <c>LazyLoadProxyInMemoryTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         Lazy loading through Castle proxies rather than through an injected
///         <c>ILazyLoader</c> — a different mechanism from the one Phase L fixed, and the one v1
///         covered with this same base. <c>Microsoft.EntityFrameworkCore.Proxies</c> and
///         <c>Castle.Core</c> arrive transitively with the specification-tests package, so this
///         adds no dependency and does not touch ADR-001.
///     </para>
///     <para>
///         The <c>Ignore</c> calls below are EF's own InMemory fixture, one for one: the backend
///         <em>is</em> the InMemory store, which has no complex types, so <c>Milk</c> and
///         <c>Culture</c> are ignored exactly where EF ignores them.
///     </para>
/// </remarks>
public class LazyLoadProxyInfoCarrierTest(LazyLoadProxyInfoCarrierTest.InfoCarrierFixture fixture)
    : LazyLoadProxyTestBase<LazyLoadProxyInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : LoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "LazyLoadProxyInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<Called>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Quest>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Entity>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Company>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Parson>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<SingleShadowFk>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Mother>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Father>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Address>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Pyrson>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<NonVirtualOneToOneOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<VirtualOneToOneOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<NonVirtualParent>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Single>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<VirtualOneToManyOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<NonVirtualOneToManyOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<ExplicitLazyLoadVirtualOneToManyOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<ExplicitLazyLoadNonVirtualOneToManyOwner>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Child>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<NonVirtualChild>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<ChildAk>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<ChildShadowFk>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<ChildCompositeKey>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<SinglePkToPk>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<SingleAk>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<SingleCompositeKey>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Nose>(b =>
            {
                b.Ignore(e => e.Milk);
                b.Ignore(e => e.Culture);
            });

            modelBuilder.Entity<Blog>(e =>
            {
                e.Ignore(x => x.Milk);
                e.Ignore(x => x.Culture);
                e.OwnsOne(
                    x => x.Writer, b =>
                    {
                        b.Ignore(w => w.Milk);
                        b.Ignore(w => w.Culture);
                    });
                e.OwnsOne(
                    x => x.Reader, b =>
                    {
                        b.Ignore(w => w.Milk);
                        b.Ignore(w => w.Culture);
                    });
                e.OwnsOne(
                    x => x.Host, b =>
                    {
                        b.Ignore(w => w.Milk);
                        b.Ignore(w => w.Culture);
                    });
            });
        }
    }
}
