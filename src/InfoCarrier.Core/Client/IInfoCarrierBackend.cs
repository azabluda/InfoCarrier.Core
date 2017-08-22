namespace InfoCarrier.Core.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore.Update;

    public interface IInfoCarrierBackend
    {
        string LogFragment { get; }

        QueryDataResult QueryData(QueryDataRequest request);

        Task<QueryDataResult> QueryDataAsync(QueryDataRequest request);

        SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries);

        Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries);

        void BeginTransaction();

        Task BeginTransactionAsync();

        void CommitTransaction();

        void RollbackTransaction();
    }
}
