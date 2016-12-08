namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using Common;
    using Common.Properties;
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

        private ITransactionManager TransactionManager =>
            this.transactionManager.ServerContext.GetServiceInterface<ITransactionManager>();

        private ILogger<InfoCarrierTransactionManager> Logger => this.transactionManager.Logger;

        public void Commit()
        {
            this.CheckActive();

            try
            {
                this.TransactionManager.CommitTransaction();
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
                this.TransactionManager.RollbackTransaction();
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
                throw new InvalidOperationException(Resources.NoActiveTransaction);
            }
        }

        private void ClearTransaction()
        {
            this.finished = true;
            this.transactionManager.ClearTransaction();
        }
    }
}