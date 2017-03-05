namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.EntityFrameworkCore.Storage.Internal;

    public class InMemoryTestStore : TestStore
    {
        private readonly Action deleteDatabase;

        public InMemoryTestStore(string storeName, Func<DbContext> dbContextFactory, Action<DbContext> seedDatabase)
        {
            using (var context = dbContextFactory())
            {
                seedDatabase(context);
                var storeSource = context.GetService<IInMemoryStoreSource>();
                this.deleteDatabase = () => storeSource.GetNamedStore(storeName).Clear();
            }
        }

        public override void Dispose()
        {
            this.deleteDatabase?.Invoke();
            base.Dispose();
        }
    }
}
