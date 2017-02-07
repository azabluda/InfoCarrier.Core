namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using System.Collections.Generic;
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

        public TestInfoCarrierBackend(Func<DbContext> dbContextFactory)
        {
            this.dbContextFactory = dbContextFactory;
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
            using (var helper = new SaveChangesHelper(this.dbContextFactory, new SaveChangesRequest(entries)))
            {
                return helper.SaveChanges();
            }
        }

        public Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries)
        {
            using (var helper = new SaveChangesHelper(this.dbContextFactory, new SaveChangesRequest(entries)))
            {
                return helper.SaveChangesAsync();
            }
        }
    }
}