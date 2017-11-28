// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.ServiceModel;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common;

    [ServiceContract]
    public interface IWcfService
    {
        [OperationContract]
        QueryDataResult ProcessQueryDataRequest(QueryDataRequest request);

        [OperationContract(Name = nameof(ProcessQueryDataRequestAsync))]
        Task<QueryDataResult> ProcessQueryDataRequestAsync(QueryDataRequest request);

        [OperationContract]
        SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request);

        [OperationContract(Name = nameof(ProcessSaveChangesRequestAsync))]
        Task<SaveChangesResult> ProcessSaveChangesRequestAsync(SaveChangesRequest request);
    }
}
