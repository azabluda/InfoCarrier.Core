// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    ///     <para>
    ///         Represents a connection with a remote service. The implementation should be passed
    ///         to <see cref="InfoCarrierDbContextOptionsExtensions.UseInfoCarrierBackend{TContext}"/>
    ///         method to configure the context to interact with that remote service.
    ///     </para>
    ///     <para>
    ///         This interface is to be implemented on the client-side of a multi-tier application.
    ///     </para>
    /// </summary>
    public interface IInfoCarrierBackend
    {
        /// <summary>
        ///     Gets the remote server address. Used for logging.
        /// </summary>
        string ServerUrl { get; }

        /// <summary>
        ///     Sends a <see cref="QueryDataRequest" /> object to the remote service for
        ///     execution of a query against the actual database.
        /// </summary>
        /// <param name="request">
        ///     The <see cref="QueryDataRequest" /> object to be sent.
        /// </param>
        /// <param name="dbContext">
        ///     The corresponding local <see cref="DbContext" /> which may contain additional
        ///     properties you might want to send to the service as well.
        /// </param>
        /// <returns>
        ///     The result of the query execution on the server-side.
        /// </returns>
        QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext);

        /// <summary>
        ///     Asynchronously sends a <see cref="QueryDataRequest" /> object to the remote service
        ///     for execution of a query against the actual database.
        /// </summary>
        /// <param name="request">
        ///     The <see cref="QueryDataRequest" /> object to be sent.
        /// </param>
        /// <param name="dbContext">
        ///     The corresponding local <see cref="DbContext" /> which may contain additional
        ///     properties you might want to send to the service as well.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the result of the query execution
        ///     on the server-side.
        /// </returns>
        Task<QueryDataResult> QueryDataAsync(
            QueryDataRequest request,
            DbContext dbContext,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Sends a <see cref="SaveChangesRequest" /> object to the remote service for
        ///     persisting the updated entities into the actual database.
        /// </summary>
        /// <param name="request">
        ///     The <see cref="SaveChangesRequest" /> object to be sent.
        /// </param>
        /// <returns>
        ///     The result of the server-side operation.
        /// </returns>
        SaveChangesResult SaveChanges(SaveChangesRequest request);

        /// <summary>
        ///     Asynchronously sends a <see cref="SaveChangesRequest" /> object to the remote
        ///     service for persisting the updated entities into the actual database.
        /// </summary>
        /// <param name="request">
        ///     The <see cref="SaveChangesRequest" /> object to be sent.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the result of the server-side operation.
        /// </returns>
        Task<SaveChangesResult> SaveChangesAsync(
            SaveChangesRequest request,
            CancellationToken cancellationToken);

        /// <summary>
        ///     Sends a command to start a transaction on the remote server
        ///     if it generally supports transactions, otherwise throws <see cref="NotSupportedException" />.
        /// </summary>
        void BeginTransaction();

        /// <summary>
        ///     Asynchronously sends a command to start a transaction on the server
        ///     if it generally supports transactions, otherwise throws <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to
        ///     complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        /// </returns>
        Task BeginTransactionAsync(CancellationToken cancellationToken);

        /// <summary>
        ///     Sends a command to commit the current transaction on the server.
        /// </summary>
        void CommitTransaction();

        /// <summary>
        ///     Sends a command to rollback the current transaction on the server.
        /// </summary>
        void RollbackTransaction();
    }
}
