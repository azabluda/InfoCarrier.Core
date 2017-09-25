namespace InfoCarrier.Core.FunctionalTests
{
    using System;
    using Microsoft.EntityFrameworkCore;

    public class SimpleInMemoryTestStore : TestStoreImplBase
    {
        public SimpleInMemoryTestStore(string databaseName)
        {
            this.DbContextOptions = new DbContextOptionsBuilder().UseInMemoryDatabase(databaseName).Options;
        }

        protected override DbContextOptions DbContextOptions { get; }

        public override TestStoreBase FromShared()
            => throw new NotImplementedException();

        protected override DbContext CreateStoreContextInternal(DbContext clientDbContext)
            => throw new NotImplementedException();
    }
}