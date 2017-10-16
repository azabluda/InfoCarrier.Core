// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace WcfSample
{
    using System;
    using System.ServiceModel;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;

    public class WcfBackendImpl : IInfoCarrierBackend
    {
        private readonly ChannelFactory<IMyRemoteService> channelFactory
            = new ChannelFactory<IMyRemoteService>(
                new BasicHttpBinding(),
                new EndpointAddress(new Uri(WcfShared.UriString)));

        // Gets the remote server address. Used for logging.
        public string ServerUrl
            => this.channelFactory.Endpoint.Address.ToString();

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
        {
            IMyRemoteService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return channel.ProcessQueryDataRequest(request);
            }
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
        {
            IMyRemoteService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return channel.ProcessSaveChangesRequest(request);
            }
        }

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void BeginTransaction() => throw new NotSupportedException();

        public Task BeginTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public void CommitTransaction() => throw new NotSupportedException();

        public void RollbackTransaction() => throw new NotSupportedException();
    }
}
