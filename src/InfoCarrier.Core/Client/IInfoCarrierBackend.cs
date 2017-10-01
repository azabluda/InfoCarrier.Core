﻿namespace InfoCarrier.Core.Client
{
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;

    public interface IInfoCarrierBackend
    {
        string ServerUrl { get; }

        QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext);

        Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext);

        ISaveChangesResult SaveChanges(SaveChangesRequest request);

        Task<ISaveChangesResult> SaveChangesAsync(SaveChangesRequest request);

        void BeginTransaction();

        Task BeginTransactionAsync();

        void CommitTransaction();

        void RollbackTransaction();
    }
}
