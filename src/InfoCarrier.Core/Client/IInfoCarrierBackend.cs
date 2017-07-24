namespace InfoCarrier.Core.Client
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Common;
    using Microsoft.EntityFrameworkCore.Update;
    using Remote.Linq.Expressions;

    public interface IInfoCarrierBackend
    {
        IEnumerable<DynamicObject> QueryData(Expression rlinq, bool trackQueryResults);

        Task<IEnumerable<DynamicObject>> QueryDataAsync(Expression rlinq, bool trackQueryResults);

        SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries);

        Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries);

        void BeginTransaction();

        Task BeginTransactionAsync();

        void CommitTransaction();

        void RollbackTransaction();
    }
}
