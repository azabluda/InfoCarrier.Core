namespace WcfSample
{
    using System.ServiceModel;
    using InfoCarrier.Core.Common;

    [ServiceContract]
    public interface IMyRemoteService
    {
        [OperationContract]
        QueryDataResult ProcessQueryDataRequest(QueryDataRequest request);

        [OperationContract]
        SaveChangesResult ProcessSaveChangesRequest(SaveChangesRequest request);
    }
}
