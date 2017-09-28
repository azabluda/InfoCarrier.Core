namespace InfoCarrier.Core.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Update;

    public interface IInfoCarrierBackend
    {
        string ServerUrl { get; }

        QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext);

        Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext);

        SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries);

        Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries);

        void BeginTransaction();

        Task BeginTransactionAsync();

        void CommitTransaction();

        void RollbackTransaction();
    }
}
