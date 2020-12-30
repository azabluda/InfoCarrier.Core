// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample.Controllers
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Properties;
    using InfoCarrier.Core.Server;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;

    [Route("api")]
    public class InfoCarrierController : ControllerBase
    {
        private readonly IInfoCarrierServer infoCarrierServer = SqlServerShared.CreateInfoCarrierServer();
        private readonly IMemoryCache cache;

        public InfoCarrierController(IMemoryCache memoryCache)
        {
            this.cache = memoryCache;
        }

        private string TransactionId => this.Request.Headers[WebApiShared.TransactionIdHeader];

        private bool ExecuteInTransaction => !string.IsNullOrEmpty(this.TransactionId);

        [HttpPost]
        [Route("QueryData")]
        public Task<QueryDataResult> PostQueryDataAsync([FromBody] QueryDataRequest request)
            => this.infoCarrierServer.QueryDataAsync(this.CreateDbContext, request);

        [HttpPost]
        [Route("SaveChanges")]
        public Task<SaveChangesResult> PostSaveChangesAsync([FromBody] SaveChangesRequest request)
            => this.infoCarrierServer.SaveChangesAsync(this.CreateDbContext, request);

        [HttpPost]
        [Route("BeginTransaction")]
        public async Task<IActionResult> PostBeginTransaction()
        {
            if (this.ExecuteInTransaction)
            {
                throw new InvalidOperationException(InfoCarrierStrings.TransactionAlreadyStarted);
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

            throw new InvalidOperationException(InfoCarrierStrings.NoActiveTransaction);
        }
    }
}
