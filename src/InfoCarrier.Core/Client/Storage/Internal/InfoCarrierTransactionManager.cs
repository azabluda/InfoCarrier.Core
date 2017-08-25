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
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Storage;

    public class InfoCarrierTransactionManager : IDbContextTransactionManager
    {
        public InfoCarrierTransactionManager(
            IDbContextOptions options)
        {
            this.InfoCarrierBackend = options.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierBackend;
        }

        internal IInfoCarrierBackend InfoCarrierBackend { get; }

        public virtual IDbContextTransaction CurrentTransaction { get; protected set; }

        public virtual IDbContextTransaction BeginTransaction()
        {
            this.CheckNoTransaction();
            this.InfoCarrierBackend.BeginTransaction();
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.CheckNoTransaction();
            await this.InfoCarrierBackend.BeginTransactionAsync();
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        public virtual void CommitTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
            }

            this.CurrentTransaction.Commit();
        }

        public virtual void RollbackTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
            }

            this.CurrentTransaction.Rollback();
        }

        private void CheckNoTransaction()
        {
            if (this.CurrentTransaction != null)
            {
                throw new InvalidOperationException(RelationalStrings.TransactionAlreadyStarted);
            }
        }

        internal void ClearTransaction()
        {
            this.CurrentTransaction?.Dispose();
            this.CurrentTransaction = null;
        }

        public void ResetState()
        {
            throw new NotImplementedException();
        }
    }
}
