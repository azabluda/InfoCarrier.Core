// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging;

    public class InfoCarrierTransactionManager : IDbContextTransactionManager
    {
        private static readonly DummyTransaction StubTransaction = new DummyTransaction();

        private readonly ILogger<InfoCarrierTransactionManager> logger;

        public InfoCarrierTransactionManager(ILogger<InfoCarrierTransactionManager> logger)
        {
            this.logger = logger;
        }

        public virtual IDbContextTransaction BeginTransaction()
        {
            return StubTransaction;
        }

        public virtual Task<IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult<IDbContextTransaction>(StubTransaction);
        }

        public virtual void CommitTransaction()
        {
        }

        public virtual void RollbackTransaction()
        {
        }

        private class DummyTransaction : IDbContextTransaction
        {
            public virtual void Commit()
            {
            }

            public virtual void Rollback()
            {
            }

            public virtual void Dispose()
            {
            }
        }
    }
}
