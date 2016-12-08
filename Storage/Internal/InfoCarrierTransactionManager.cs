// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common.Properties;
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
            this.CheckNoTransaction();
            this.ServerContext.GetServiceInterface<Common.ITransactionManager>().BeginTransaction();
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.CheckNoTransaction();
            await this.ServerContext.GetServiceInterface<Common.ITransactionManager>().BeginTransactionAsync();
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        public virtual void CommitTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException(Resources.NoActiveTransaction);
            }

            this.CurrentTransaction.Commit();
        }

        public virtual void RollbackTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException(Resources.NoActiveTransaction);
            }

            this.CurrentTransaction.Rollback();
        }

        private void CheckNoTransaction()
        {
            if (this.CurrentTransaction != null)
            {
                throw new InvalidOperationException(Resources.TransactionAlreadyStarted);
            }
        }

        internal void ClearTransaction()
        {
            this.CurrentTransaction?.Dispose();
            this.CurrentTransaction = null;
        }
    }
}
