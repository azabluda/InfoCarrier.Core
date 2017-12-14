// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample.Controllers
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.Extensions.Caching.Memory;

    [Route("api")]
    public class InfoCarrierController : ControllerBase
    {
        private readonly IMemoryCache cache;

        public InfoCarrierController(IMemoryCache memoryCache)
        {
            this.cache = memoryCache;
        }

        private string TransactionId => this.Request.Headers[WebApiShared.TransactionIdHeader];

        private bool ExecuteInTransaction => !string.IsNullOrEmpty(this.TransactionId);

        [HttpPost]
        [Route("QueryData")]
        public async Task<QueryDataResult> PostQueryDataAsync([FromBody] QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(this.CreateDbContext, request))
            {
                return await helper.QueryDataAsync();
            }
        }

        [HttpPost]
        [Route("SaveChanges")]
        public async Task<SaveChangesResult> PostSaveChangesAsync([FromBody] SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(this.CreateDbContext, request))
            {
                return await helper.SaveChangesAsync();
            }
        }

        [HttpPost]
        [Route("BeginTransaction")]
        public async Task<IActionResult> PostBeginTransaction()
        {
            if (this.ExecuteInTransaction)
            {
                throw new InvalidOperationException(RelationalStrings.TransactionAlreadyStarted);
            }

            var options = new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(10) };
            options.RegisterPostEvictionCallback(
                (key, value, reason, state) =>
                {
                    if (value is DbTransaction transactionValue)
                    {
                        Console.WriteLine($"Dispose transaction {key}.");
                        transactionValue.Connection?.Dispose();
                    }
                });

            string transactionId = Guid.NewGuid().ToString();
            Console.WriteLine($"Create transaction {transactionId}.");

            var connection = new SqlConnection { ConnectionString = SqlServerShared.ConnectionString };
            await connection.OpenAsync();
            DbTransaction transaction = connection.BeginTransaction();

            this.cache.Set(transactionId, transaction, options);
            return this.Content(transactionId);
        }

        [HttpPost]
        [Route("CommitTransaction")]
        public void PostCommitTransaction()
        {
            this.EndTransaction(t => t.Commit());
        }

        [HttpPost]
        [Route("RollbackTransaction")]
        public void PostRollbackTransaction()
        {
            this.EndTransaction(t => t.Rollback());
        }

        private void EndTransaction(Action<DbTransaction> endAction)
        {
            if (!this.ExecuteInTransaction)
            {
                return;
            }

            DbTransaction transaction = this.GetOpenTransaction();
            DbConnection connection = transaction.Connection;
            endAction(transaction);
            connection.Close();
            this.cache.Remove(this.TransactionId);
        }

        private DbContext CreateDbContext()
        {
            if (!this.ExecuteInTransaction)
            {
                return SqlServerShared.CreateDbContext();
            }

            DbTransaction transaction = this.GetOpenTransaction();
            DbContext dbContext = SqlServerShared.CreateDbContext(transaction.Connection);
            dbContext.Database.UseTransaction(transaction);
            return dbContext;
        }

        private DbTransaction GetOpenTransaction()
        {
            if (this.cache.TryGetValue(this.TransactionId, out DbTransaction transaction) && transaction.Connection != null)
            {
                return transaction;
            }

            throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
        }
    }
}
