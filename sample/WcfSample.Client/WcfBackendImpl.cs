// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace WcfSample
{
    using System;
    using System.ServiceModel;
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

        // Service URL string (used for logging)
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

        public Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext)
            => throw new NotImplementedException();

        public Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request)
            => throw new NotImplementedException();

        public void BeginTransaction() => throw new NotImplementedException();

        public Task BeginTransactionAsync() => throw new NotImplementedException();

        public void CommitTransaction() => throw new NotImplementedException();

        public void RollbackTransaction() => throw new NotImplementedException();
    }
}
