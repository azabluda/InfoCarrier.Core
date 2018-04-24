// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Client;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Server;

    public class TestInfoCarrierBackend : IInfoCarrierBackend, IDisposable
    {
        private readonly InfoCarrierBackendTestStore backendStore;

        public TestInfoCarrierBackend(InfoCarrierBackendTestStore backendStore)
        {
            this.backendStore = backendStore;
        }

        public string ServerUrl => this.backendStore.Name;

        public void Initialize(Action<DbContext> seed)
        {
            this.backendStore.Initialize(this.backendStore.ServiceProvider, this.backendStore.CreateDbContext, seed);
        }

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
        {
            using (var helper = new QueryDataHelper(this.backendStore.CreateDbContext, request))
            {
                return helper.QueryData();
            }
        }

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
        {
            using (var helper = new QueryDataHelper(this.backendStore.CreateDbContext, request))
            {
                return await helper.QueryDataAsync(cancellationToken);
            }
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(this.backendStore.CreateDbContext, request))
            {
                return helper.SaveChanges();
            }
        }

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
        {
            using (var helper = new SaveChangesHelper(this.backendStore.CreateDbContext, request))
            {
                return await helper.SaveChangesAsync(cancellationToken);
            }
        }

        public void BeginTransaction()
        {
            this.backendStore.BeginTransaction();
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            return this.backendStore.BeginTransactionAsync(cancellationToken);
        }

        public void CommitTransaction()
        {
            this.backendStore.CommitTransaction();
        }

        public void RollbackTransaction()
        {
            this.backendStore.RollbackTransaction();
        }

        public void Clean()
        {
            using (var dbContext = this.backendStore.CreateDbContext())
            {
                this.backendStore.Clean(dbContext);
            }
        }

        public void Dispose()
        {
            this.backendStore.Dispose();
        }
    }
}
