// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample.Controllers
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.Extensions.Caching.Memory;

    [Route("api")]
    public class InfoCarrierController : Controller
    {
        private readonly IMemoryCache cache;

        public InfoCarrierController(IMemoryCache memoryCache)
        {
            this.cache = memoryCache;
        }

        private string ClientId => this.Request.Headers["InfoCarrierClientId"];

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
        public async Task PostBeginTransactionAsync()
        {
            var sessionEntry = this.GetSessionEntry();
            if (sessionEntry.DbTransaction != null)
            {
                throw new TransactionException(RelationalStrings.TransactionAlreadyStarted);
            }

            await sessionEntry.DbConnection.OpenAsync();
            sessionEntry.DbTransaction = sessionEntry.DbConnection.BeginTransaction();
        }

        [HttpPost]
        [Route("CommitTransaction")]
        public void PostCommitTransaction()
        {
            var sessionEntry = this.GetSessionEntry();
            if (sessionEntry.DbTransaction == null)
            {
                throw new TransactionException(RelationalStrings.NoActiveTransaction);
            }

            sessionEntry.DbTransaction.Commit();
            sessionEntry.DbTransaction = null;
            sessionEntry.DbConnection.Close();
        }

        [HttpPost]
        [Route("RollbackTransaction")]
        public void PostRollbackTransaction()
        {
            var sessionEntry = this.GetSessionEntry();
            if (sessionEntry.DbTransaction == null)
            {
                throw new TransactionException(RelationalStrings.NoActiveTransaction);
            }

            sessionEntry.DbTransaction.Rollback();
            sessionEntry.DbTransaction = null;
            sessionEntry.DbConnection.Close();
        }

        private DbContext CreateDbContext()
        {
            SessionEntry sessionEntry = this.GetSessionEntry();
            DbContext dbContext = SqlServerShared.CreateDbContext(sessionEntry.DbConnection);
            dbContext.Database.UseTransaction(sessionEntry.DbTransaction);
            return dbContext;
        }

        private SessionEntry GetSessionEntry()
            => this.cache.GetOrCreate(
                this.ClientId,
                entry =>
                {
                    entry.SlidingExpiration = TimeSpan.FromSeconds(10);
                    entry.Priority = CacheItemPriority.Low;
                    entry.RegisterPostEvictionCallback(
                        (key, value, reason, state) =>
                        {
                            if (value is SessionEntry sessionEntry)
                            {
                                Console.WriteLine($"Dispose session {key}.");
                                sessionEntry.Dispose();
                            }
                        });

                    Console.WriteLine($"Create session {this.ClientId}.");
                    return new SessionEntry();
                });

        private sealed class SessionEntry : IDisposable
        {
            public SessionEntry()
            {
                this.DbConnection = new SqlConnection { ConnectionString = SqlServerShared.ConnectionString };
            }

            public DbConnection DbConnection { get; }

            public DbTransaction DbTransaction { get; set; }

            public void Dispose()
                => this.DbConnection.Dispose();
        }
    }
}
