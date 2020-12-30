// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Properties;
    using InfoCarrier.Core.Server;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using ServiceStack;

    [Authenticate]
    public class DataService : Service
    {
        private readonly IInfoCarrierServer infoCarrierServer = SqlServerShared.CreateInfoCarrierServer();

        private DbTransaction SessionDbTransaction
        {
            get => this.SessionAs<UserSession>()?.DbTransaction;

            set
            {
                var sess = this.SessionAs<UserSession>();
                if (sess != null)
                {
                    sess.DbTransaction = value;
                }
            }
        }

        public async Task<QueryDataResponse> Any(QueryData request)
            => new QueryDataResponse(await this.infoCarrierServer.QueryDataAsync(this.CreateContext, request.Request));

        public async Task<SaveChangesResponse> Any(SaveChanges request)
            => new SaveChangesResponse(await this.infoCarrierServer.SaveChangesAsync(this.CreateContext, request.Request));

        public async Task Any(BeginTransaction request)
        {
            if (this.SessionDbTransaction != null)
            {
                throw new InvalidOperationException(InfoCarrierStrings.TransactionAlreadyStarted);
            }

            var connection = new SqlConnection { ConnectionString = SqlServerShared.ConnectionString };
            await connection.OpenAsync();
            this.SessionDbTransaction = connection.BeginTransaction();
        }

        public void Any(CommitTransaction request)
        {
            this.EndTransaction(t => t.Commit());
        }

        public void Any(RollbackTransaction request)
        {
            this.EndTransaction(t => t.Rollback());
        }

        private void EndTransaction(Action<DbTransaction> endAction)
        {
            if (this.SessionDbTransaction == null)
            {
                throw new InvalidOperationException(InfoCarrierStrings.NoActiveTransaction);
            }

            var conn = this.SessionDbTransaction.Connection;
            endAction(this.SessionDbTransaction);
            conn.Close();
            this.SessionDbTransaction = null;
        }

        private BloggingContext CreateContext()
        {
            BloggingContext ctx = SqlServerShared.CreateDbContext(this.SessionDbTransaction?.Connection);
            ctx.Database.UseTransaction(this.SessionDbTransaction);
            return ctx;
        }
    }
}
