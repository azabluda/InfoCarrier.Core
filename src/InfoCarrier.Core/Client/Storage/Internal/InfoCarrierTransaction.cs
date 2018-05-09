// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.EntityFrameworkCore.Internal;
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
                this.transactionManager.InfoCarrierBackend.CommitTransaction();
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
                this.transactionManager.InfoCarrierBackend.RollbackTransaction();
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
