// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Properties;
    using Microsoft.EntityFrameworkCore.Storage;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    internal sealed class InfoCarrierTransaction : IDbContextTransaction
    {
        private readonly InfoCarrierTransactionManager transactionManager;
        private bool finished;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierTransaction(InfoCarrierTransactionManager transactionManager)
        {
            this.transactionManager = transactionManager;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Entity Framework Core internal.")]
        public Guid TransactionId { get; } = Guid.NewGuid();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public void Commit()
        {
            this.CheckActive();

            try
            {
                this.transactionManager.InfoCarrierClient.CommitTransaction();
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            this.CheckActive();

            try
            {
                await this.transactionManager.InfoCarrierClient.CommitTransactionAsync(cancellationToken);
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public void Rollback()
        {
            this.CheckActive();

            try
            {
                this.transactionManager.InfoCarrierClient.RollbackTransaction();
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            this.CheckActive();

            try
            {
                await this.transactionManager.InfoCarrierClient.RollbackTransactionAsync(cancellationToken);
            }
            finally
            {
                this.ClearTransaction();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public void Dispose()
        {
            if (!this.finished)
            {
                this.Rollback();
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public async ValueTask DisposeAsync()
        {
            if (!this.finished)
            {
                await this.RollbackAsync(default);
            }
        }

        private void CheckActive()
        {
            if (this.finished)
            {
                throw new InvalidOperationException(InfoCarrierStrings.NoActiveTransaction);
            }
        }

        private void ClearTransaction()
        {
            this.finished = true;
            this.transactionManager.ClearTransaction();
        }
    }
}
