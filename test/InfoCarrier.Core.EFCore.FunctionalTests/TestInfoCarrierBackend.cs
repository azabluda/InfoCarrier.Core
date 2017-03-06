namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Client;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Update;
    using Remote.Linq.Expressions;
    using Server;

    public class TestInfoCarrierBackend : IInfoCarrierBackend
    {
        private readonly Func<DbContext> dbContextFactory;
        private readonly bool isInMemoryDatabase;

        public TestInfoCarrierBackend(Func<DbContext> dbContextFactory, bool isInMemoryDatabase)
        {
            this.dbContextFactory = dbContextFactory;
            this.isInMemoryDatabase = isInMemoryDatabase;
        }

        public void BeginTransaction()
        {
            this.dbContextFactory().Database.BeginTransaction();
        }

        public Task BeginTransactionAsync()
        {
            return this.dbContextFactory().Database.BeginTransactionAsync();
        }

        public void CommitTransaction()
        {
            this.dbContextFactory().Database.CommitTransaction();
        }

        public IEnumerable<DynamicObject> QueryData(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.dbContextFactory, rlinq))
            {
                return helper.QueryData();
            }
        }

        public async Task<IEnumerable<DynamicObject>> QueryDataAsync(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.dbContextFactory, rlinq))
            {
                return await helper.QueryDataAsync();
            }
        }

        public void RollbackTransaction()
        {
            this.dbContextFactory().Database.RollbackTransaction();
        }

        public SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                return helper.SaveChanges();
            }
        }

        public Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                return helper.SaveChangesAsync();
            }
        }

        private SaveChangesHelper CreateSaveChangesHelper(IEnumerable<IUpdateEntry> entries)
        {
            var helper = new SaveChangesHelper(this.dbContextFactory, new SaveChangesRequest(entries));

            if (this.isInMemoryDatabase)
            {
                // Temporary values for Key properties generated on the client side should
                // be treated a permanent if the backend database is InMemory
                var tempKeyProps =
                    helper.Entries.SelectMany(e =>
                        e.ToEntityEntry().Properties
                            .Where(p => p.IsTemporary && p.Metadata.IsKey())).ToList();

                tempKeyProps.ForEach(p => p.IsTemporary = false);
            }

            return helper;
        }
    }
}