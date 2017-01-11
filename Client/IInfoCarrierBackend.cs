namespace InfoCarrier.Core.Client
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Common;
    using Microsoft.EntityFrameworkCore.Update;
    using Remote.Linq.Expressions;

    public interface IInfoCarrierBackend
    {
        IEnumerable<DynamicObject> QueryData(Expression rlinq);

        Task<IEnumerable<DynamicObject>> QueryDataAsync(Expression rlinq);

        SaveChangesResult SaveChanges(SaveChangesRequest request, Func<int, IUpdateEntry> entityByIndex);

        Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, Func<int, IUpdateEntry> entityByIndex);

        void BeginTransaction();

        Task BeginTransactionAsync();

        void CommitTransaction();

        void RollbackTransaction();
    }
}
