// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
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
        private readonly ChannelFactory<IWcfService> channelFactory;

        public WcfBackendImpl(string serverUrl = null)
        {
            this.channelFactory = new ChannelFactory<IWcfService>(
                new BasicHttpBinding { MaxReceivedMessageSize = WcfShared.MaxReceivedMessageSize },
                new EndpointAddress(new Uri($"http://{serverUrl ?? WcfShared.BaseUrl}/{WcfShared.ServiceName}")));
        }

        // Gets the remote server address. Used for logging.
        public string ServerUrl
            => this.channelFactory.Endpoint.Address.ToString();

        public virtual QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
        {
            IWcfService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return channel.ProcessQueryDataRequest(request);
            }
        }

        public virtual SaveChangesResult SaveChanges(SaveChangesRequest request)
        {
            IWcfService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return channel.ProcessSaveChangesRequest(request);
            }
        }

        public virtual async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
        {
            IWcfService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return await channel.ProcessQueryDataRequestAsync(request);
            }
        }

        public virtual async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
        {
            IWcfService channel = this.channelFactory.CreateChannel();
            using ((IDisposable)channel)
            {
                return await channel.ProcessSaveChangesRequestAsync(request);
            }
        }

        public virtual void BeginTransaction() => throw new NotSupportedException();

        public virtual Task BeginTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public virtual void CommitTransaction() => throw new NotSupportedException();

        public virtual void RollbackTransaction() => throw new NotSupportedException();
    }
}
