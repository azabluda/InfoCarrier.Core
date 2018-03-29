// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using ServiceStack;

    public class ServiceStackBackend : IInfoCarrierBackend
    {
        private readonly JsonHttpClient client;

        public ServiceStackBackend(JsonHttpClient client)
        {
            this.client = client;
        }

        public string ServerUrl => this.client.BaseUri;

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
        {
            return this.client.Send(new QueryData(request)).Result;
        }

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
        {
            return (await this.client.SendAsync(new QueryData(request), cancellationToken)).Result;
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
        {
            return this.client.Send(new SaveChanges(request)).Result;
        }

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
        {
            return (await this.client.SendAsync(new SaveChanges(request), cancellationToken)).Result;
        }

        public void BeginTransaction()
        {
            this.client.Send(new BeginTransaction());
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            await this.client.SendAsync(new BeginTransaction(), cancellationToken);
        }

        public void CommitTransaction()
        {
            this.client.Send(new CommitTransaction());
        }

        public void RollbackTransaction()
        {
            this.client.Send(new RollbackTransaction());
        }
    }
}
