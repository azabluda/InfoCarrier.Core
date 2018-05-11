// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Storage;

    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InfoCarrierTransactionManager : IDbContextTransactionManager
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1642:ConstructorSummaryDocumentationMustBeginWithStandardText", Justification = "Entity Framework Core internal.")]
        public InfoCarrierTransactionManager(IDbContextOptions options)
        {
            this.InfoCarrierBackend = options.Extensions.OfType<InfoCarrierOptionsExtension>().First().InfoCarrierBackend;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Entity Framework Core internal.")]
        internal IInfoCarrierBackend InfoCarrierBackend { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1623:PropertySummaryDocumentationMustMatchAccessors", Justification = "Entity Framework Core internal.")]
        public virtual IDbContextTransaction CurrentTransaction { get; protected set; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public virtual IDbContextTransaction BeginTransaction()
        {
            this.CheckNoTransaction();
            this.InfoCarrierBackend.BeginTransaction();
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            this.CheckNoTransaction();
            await this.InfoCarrierBackend.BeginTransactionAsync(cancellationToken);
            return this.CurrentTransaction = new InfoCarrierTransaction(this);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual void CommitTransaction()
        {
            if (this.CurrentTransaction == null)
            {
                throw new InvalidOperationException(RelationalStrings.NoActiveTransaction);
            }

            this.CurrentTransaction.Commit();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
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

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        internal void ClearTransaction()
        {
            this.CurrentTransaction?.Dispose();
            this.CurrentTransaction = null;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public void ResetState()
        {
            throw new NotImplementedException();
        }
    }
}
