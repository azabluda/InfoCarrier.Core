// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.ServiceModel;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;
    using InfoCarrier.Core.Server;

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class InfoCarrierService : IWcfService
    {
        public QueryDataResult ProcessQueryDataRequest(QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(SqlServerShared.CreateDbContext, request))
            {
                return helper.QueryData();
            }
        }

        public async Task<QueryDataResult> ProcessQueryDataRequestAsync(QueryDataRequest request)
        {
            using (var helper = new QueryDataHelper(SqlServerShared.CreateDbContext, request))
            {
                return await helper.QueryDataAsync();
            }
        }

        public SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(SqlServerShared.CreateDbContext, request))
            {
                return helper.SaveChanges();
            }
        }

        public async Task<SaveChangesResult> ProcessSaveChangesRequestAsync(SaveChangesRequest request)
        {
            using (var helper = new SaveChangesHelper(SqlServerShared.CreateDbContext, request))
            {
                return await helper.SaveChangesAsync();
            }
        }
    }
}