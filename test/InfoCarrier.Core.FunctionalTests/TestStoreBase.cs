namespace InfoCarrier.Core.FunctionalTests
{
    using Client;
    using Microsoft.EntityFrameworkCore;

    public abstract class TestStoreBase : TestStore
    {
        public abstract IInfoCarrierBackend InfoCarrierBackend { get; }

        public abstract TDbContext CreateContext<TDbContext>(DbContextOptions dbContextOptions)
            where TDbContext : DbContext;

        protected class Decorator : TestStoreBase
        {
            private readonly TestStoreBase decorated;

            public Decorator(TestStoreBase decorated)
            {
                this.decorated = decorated;
            }

            public override IInfoCarrierBackend InfoCarrierBackend => this.decorated.InfoCarrierBackend;

            public override TDbContext CreateContext<TDbContext>(DbContextOptions dbContextOptions)
                => this.decorated.CreateContext<TDbContext>(dbContextOptions);
        }
    }
}
