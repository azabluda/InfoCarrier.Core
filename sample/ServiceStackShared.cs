// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

#pragma warning disable SA1402 // FileMayOnlyContainASingleType

namespace InfoCarrierSample
{
    using System;
    using InfoCarrier.Core.Common;
    using Newtonsoft.Json;
    using ServiceStack;

    public static class ServiceStackShared
    {
        public static string BaseAddress { get; } =
            Environment.GetEnvironmentVariable(@"ServiceStack__DefaultBaseAddress")
                ?? @"http://localhost:11337/";
    }

    public abstract class HasData
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings().ConfigureInfoCarrier();

        public string Data { get; set; }

        protected T FromData<T>()
        {
            return JsonConvert.DeserializeObject<T>(this.Data, JsonSerializerSettings);
        }

        protected void ToData<T>(T obj)
        {
            this.Data = JsonConvert.SerializeObject(obj, JsonSerializerSettings);
        }
    }

    public class QueryData : HasData, IReturn<QueryDataResponse>
    {
        public QueryData(QueryDataRequest request)
        {
            this.ToData(request);
        }

        public QueryDataRequest Request => this.FromData<QueryDataRequest>();
    }

    public class QueryDataResponse : HasData
    {
        public QueryDataResponse(QueryDataResult result)
        {
            this.ToData(result);
        }

        public QueryDataResult Result => this.FromData<QueryDataResult>();
    }

    public class SaveChanges : HasData, IReturn<SaveChangesResponse>
    {
        public SaveChanges(SaveChangesRequest request)
        {
            this.ToData(request);
        }

        public SaveChangesRequest Request => this.FromData<SaveChangesRequest>();
    }

    public class SaveChangesResponse : HasData
    {
        public SaveChangesResponse(SaveChangesResult result)
        {
            this.ToData(result);
        }

        public SaveChangesResult Result => this.FromData<SaveChangesResult>();
    }

    public class BeginTransaction : IReturnVoid
    {
    }

    public class CommitTransaction : IReturnVoid
    {
    }

    public class RollbackTransaction : IReturnVoid
    {
    }
}
