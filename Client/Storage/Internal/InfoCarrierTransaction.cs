namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging;

    internal sealed class InfoCarrierTransaction : IDbContextTransaction
    {
        private readonly InfoCarrierTransactionManager transactionManager;
        private bool finished;

        public InfoCarrierTransaction(InfoCarrierTransactionManager transactionManager)
        {
            this.transactionManager = transactionManager;
        }

        private ILogger<InfoCarrierTransactionManager> Logger => this.transactionManager.Logger;

        public void Commit()
        {
            this.CheckActive();

            try
            {
                this.transactionManager.InfoCarrierBackend.CommitTransaction();
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        public void Rollback()
        {
            this.CheckActive();

            try
            {
                this.transactionManager.InfoCarrierBackend.RollbackTransaction();
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        public void Dispose()
        {
            if (!this.finished)
            {
                this.Rollback();
            }
        }

        private void CheckActive()
        {
            if (this.finished)
            {
                throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
            }
        }

        private void ClearTransaction()
        {
            this.finished = true;
            this.transactionManager.ClearTransaction();
        }
    }
}