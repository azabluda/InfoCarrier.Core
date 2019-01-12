// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.ServiceModel;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;
    using Microsoft.EntityFrameworkCore;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class InfoCarrierService : IWcfService
    {
        private readonly IInfoCarrierServer infoCarrierServer = SqlServerShared.CreateInfoCarrierServer();

        public QueryDataResult ProcessQueryDataRequest(QueryDataRequest request)
            => this.infoCarrierServer.QueryData(this.CreateDbContext, request);

        public Task<QueryDataResult> ProcessQueryDataRequestAsync(QueryDataRequest request)
            => this.infoCarrierServer.QueryDataAsync(this.CreateDbContext, request);

        public SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request)
            => this.infoCarrierServer.SaveChanges(this.CreateDbContext, request);

        public Task<SaveChangesResult> ProcessSaveChangesRequestAsync(SaveChangesRequest request)
            => this.infoCarrierServer.SaveChangesAsync(this.CreateDbContext, request);

        private DbContext CreateDbContext() => SqlServerShared.CreateDbContext();
    }
}
