// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Server;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Internal;
    using ServiceStack;

    [Authenticate]
    public class DataService : Service
    {
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
        {
            using (var helper = new QueryDataHelper(this.CreateContext, request.Request))
            {
                return new QueryDataResponse(await helper.QueryDataAsync());
            }
        }

        public async Task<SaveChangesResponse> Any(SaveChanges request)
        {
            using (var helper = new SaveChangesHelper(this.CreateContext, request.Request))
            {
                return new SaveChangesResponse(await helper.SaveChangesAsync());
            }
        }

        public async Task Any(BeginTransaction request)
        {
            if (this.SessionDbTransaction != null)
            {
                throw new InvalidOperationException(RelationalStrings.TransactionAlreadyStarted);
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
                throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
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
