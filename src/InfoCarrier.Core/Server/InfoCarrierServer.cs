// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Common.ValueMapping;
    using Microsoft.EntityFrameworkCore;

    /// <summary>
    /// Implementation for <see cref="IInfoCarrierServer"/> interface.
    /// </summary>
    public class InfoCarrierServer : IInfoCarrierServer
    {
        private readonly IEnumerable<IInfoCarrierValueMapper> customValueMappers;

        /// <summary>
        /// Initializes a new instance of the <see cref="InfoCarrierServer"/> class.
        /// </summary>
        /// <param name="customValueMappers"> Custom value mappers. </param>
        public InfoCarrierServer(IEnumerable<IInfoCarrierValueMapper> customValueMappers = null)
        {
            this.customValueMappers = customValueMappers ?? Enumerable.Empty<IInfoCarrierValueMapper>();
        }

        /// <summary>
        ///     Executes the requested query against the actual database.
        /// </summary>
        /// <param name="dbContextFactory">
        ///     Factory for <see cref="DbContext" /> against which the requested query will be executed.
        /// </param>
        /// <param name="request">
        ///     The <see cref="QueryDataRequest" /> object from the client containing the query.
        /// </param>
        /// <returns>
        ///     The result of the query execution.
        /// </returns>
        public QueryDataResult QueryData(Func<DbContext> dbContextFactory, QueryDataRequest request)
        {
            using (DbContext dbContext = dbContextFactory())
            {
                return new QueryDataHelper(dbContext, request, this.customValueMappers).QueryData();
            }
        }

        /// <summary>
        ///     Asynchronously executes the requested query against the actual database.
        /// </summary>
        /// <param name="dbContextFactory">
        ///     Factory for <see cref="DbContext" /> against which the requested query will be executed.
        /// </param>
        /// <param name="request">
        ///     The <see cref="QueryDataRequest" /> object from the client containing the query.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the result of the query execution.
        /// </returns>
        public async Task<QueryDataResult> QueryDataAsync(Func<DbContext> dbContextFactory, QueryDataRequest request, CancellationToken cancellationToken = default)
        {
            using (DbContext dbContext = dbContextFactory())
            {
                return await new QueryDataHelper(dbContext, request, this.customValueMappers).QueryDataAsync(cancellationToken);
            }
        }

        /// <summary>
        ///     Saves the updated entities into the actual database.
        /// </summary>
        /// <param name="dbContextFactory">
        ///     Factory for <see cref="DbContext" /> to save the updated entities into.
        /// </param>
        /// <param name="request">
        ///     The <see cref="SaveChangesRequest" /> object from the client containing the updated entities.
        /// </param>
        /// <returns>
        ///     The save operation result which can either be
        ///     a SaveChangesResult.Success or SaveChangesResult.Error.
        /// </returns>
        public SaveChangesResult SaveChanges(Func<DbContext> dbContextFactory, SaveChangesRequest request)
        {
            using (DbContext dbContext = dbContextFactory())
            {
                return new SaveChangesHelper(dbContext, request).SaveChanges();
            }
        }

        /// <summary>
        ///     Asynchronously saves the updated entities into the actual database.
        /// </summary>
        /// <param name="dbContextFactory">
        ///     Factory for <see cref="DbContext" /> to save the updated entities into.
        /// </param>
        /// <param name="request">
        ///     The <see cref="SaveChangesRequest" /> object from the client containing the updated entities.
        /// </param>
        /// <param name="cancellationToken">
        ///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation.
        ///     The task result contains the save operation result which can either be
        ///     a SaveChangesResult.Success or SaveChangesResult.Error.
        /// </returns>
        public async Task<SaveChangesResult> SaveChangesAsync(Func<DbContext> dbContextFactory, SaveChangesRequest request, CancellationToken cancellationToken = default)
        {
            using (DbContext dbContext = dbContextFactory())
            {
                return await new SaveChangesHelper(dbContext, request).SaveChangesAsync(cancellationToken);
            }
        }
    }
}
