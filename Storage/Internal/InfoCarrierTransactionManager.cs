// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging;

    public class InfoCarrierTransactionManager : IDbContextTransactionManager
    {
        public InfoCarrierTransactionManager(
            IDbContextOptions options,
            ILogger<InfoCarrierTransactionManager> logger)
        {
            this.Logger = logger;
            this.ServerContext = options.Extensions.OfType<InfoCarrierOptionsExtension>().First().ServerContext;
        }

        internal ServerContext ServerContext { get; }

        internal ILogger<InfoCarrierTransactionManager> Logger { get; }

        public virtual IDbContextTransaction CurrentTransaction { get; protected set; }

        public virtual IDbContextTransaction BeginTransaction()
        {
            if (this.CurrentTransaction != null)
            {
                throw new InvalidOperationException("RelationalStrings.TransactionAlreadyStarted");
            }

            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public virtual void CommitTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException("RelationalStrings.NoActiveTransaction");
            }

            this.CurrentTransaction.Commit();
        }

        public virtual void RollbackTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException("RelationalStrings.NoActiveTransaction");
            }

            this.CurrentTransaction.Rollback();
        }

        internal void ClearTransaction()
        {
            this.CurrentTransaction?.Dispose();
            this.CurrentTransaction = null;
        }
    }
}
