namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading.Tasks;
    using Aqua.Dynamic;
    using Client;
    using Common;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Update;
    using Newtonsoft.Json;
    using Remote.Linq;
    using Remote.Linq.Expressions;
    using Server;

    public class TestInfoCarrierBackend : IInfoCarrierBackend
    {
        private readonly Func<DbContext> dbContextFactory;
        private readonly bool isInMemoryDatabase;
        private readonly DbConnection dbConnection;
        private DbTransaction transaction;

        public TestInfoCarrierBackend(Func<DbContext> dbContextFactory, bool isInMemoryDatabase, DbConnection dbConnection)
        {
            this.dbContextFactory = () =>
            {
                DbContext context = dbContextFactory();
                if (this.dbConnection != null)
                {
                    context.Database.UseTransaction(this.transaction);
                }

                return context;
            };

            this.isInMemoryDatabase = isInMemoryDatabase;
            this.dbConnection = dbConnection;
        }

        public void BeginTransaction()
        {
            if (this.dbConnection == null)
            {
                return;
            }

            this.dbConnection.Open();
            this.transaction = this.dbConnection.BeginTransaction();
        }

        public async Task BeginTransactionAsync()
        {
            if (this.dbConnection == null)
            {
                return;
            }

            await this.dbConnection.OpenAsync();
            this.transaction = this.dbConnection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            if (this.dbConnection == null)
            {
                return;
            }

            this.transaction.Commit();
            this.transaction = null;
            this.dbConnection.Close();
        }

        public IEnumerable<DynamicObject> QueryData(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.dbContextFactory, SimulateNetworkTransferJson(rlinq)))
            {
                return SimulateNetworkTransferJson(helper.QueryData());
            }
        }

        public async Task<IEnumerable<DynamicObject>> QueryDataAsync(Expression rlinq)
        {
            using (var helper = new QueryDataHelper(this.dbContextFactory, SimulateNetworkTransferJson(rlinq)))
            {
                return SimulateNetworkTransferJson(await helper.QueryDataAsync());
            }
        }

        public void RollbackTransaction()
        {
            if (this.dbConnection == null)
            {
                return;
            }

            this.transaction.Rollback();
            this.transaction = null;
            this.dbConnection.Close();
        }

        public SaveChangesResult SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                try
                {
                    return SimulateNetworkTransferJson(helper.SaveChanges());
                }
                catch (DbUpdateException e)
                {
                    SimulateNetworkTransferException(e, helper, entries);
                    throw;
                }
            }
        }

        public async Task<SaveChangesResult> SaveChangesAsync(IReadOnlyList<IUpdateEntry> entries)
        {
            using (SaveChangesHelper helper = this.CreateSaveChangesHelper(entries))
            {
                try
                {
                    return SimulateNetworkTransferJson(await helper.SaveChangesAsync());
                }
                catch (DbUpdateException e)
                {
                    SimulateNetworkTransferException(e, helper, entries);
                    throw;
                }
            }
        }

        private SaveChangesHelper CreateSaveChangesHelper(IEnumerable<IUpdateEntry> entries)
        {
            var request = SimulateNetworkTransferJson(new SaveChangesRequest(entries));
            var helper = new SaveChangesHelper(this.dbContextFactory, request);

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

        private static T SimulateNetworkTransferJson<T>(T value)
        {
            if (value == null)
            {
                return default(T);
            }

            var serializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();
            var json = JsonConvert.SerializeObject(value, serializerSettings);
            return (T)JsonConvert.DeserializeObject(json, value.GetType(), serializerSettings);
        }

        private static void SimulateNetworkTransferException(
            DbUpdateException dbUpdateException,
            SaveChangesHelper helper,
            IReadOnlyList<IUpdateEntry> entries)
        {
            var map = helper.Entries
                .Select((e, i) => new { Index = i, Entry = e })
                .ToDictionary(x => x.Entry, x => x.Index);

            var entityIndexes = dbUpdateException.Entries.Select(re => map[re.GetInfrastructure()]).ToArray();

            IReadOnlyList<IUpdateEntry> failedEntries = entityIndexes.Select(x => entries[x]).ToList();

            if (dbUpdateException is DbUpdateConcurrencyException)
            {
                throw new DbUpdateConcurrencyException(dbUpdateException.Message, failedEntries);
            }

            throw failedEntries.Any()
                ? new DbUpdateException(dbUpdateException.Message, dbUpdateException.InnerException, failedEntries)
                : new DbUpdateException(dbUpdateException.Message, dbUpdateException.InnerException);
        }
    }
}