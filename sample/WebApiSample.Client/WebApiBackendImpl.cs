// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;
    using Remote.Linq;

    internal class WebApiBackendImpl : IInfoCarrierBackend
    {
        private readonly HttpClient client = new HttpClient { BaseAddress = new Uri(WebApiShared.BaseAddress) };

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();

        public string ServerUrl
            => this.client.BaseAddress.ToString();

        public void BeginTransaction()
            => this.BeginTransactionAsync(default).Wait(); // Watchout: https://stackoverflow.com/a/35831540

        public Task BeginTransactionAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public void CommitTransaction()
            => throw new NotImplementedException();

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
            => this.QueryDataAsync(request, dbContext, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
            => await this.CallApiAsync<QueryDataRequest, QueryDataResult>("QueryData", request, cancellationToken);

        public void RollbackTransaction()
            => throw new NotImplementedException();

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
            => this.SaveChangesAsync(request, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
            => await this.CallApiAsync<SaveChangesRequest, SaveChangesResult>("SaveChanges", request, cancellationToken);

        private async Task<TResult> CallApiAsync<TRequest, TResult>(string action, TRequest request, CancellationToken cancellationToken)
        {
            string requestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
            HttpResponseMessage response = await this.client.PostAsync(
                $"api/{action}",
                new StringContent(requestJson, Encoding.UTF8, "application/json"),
                cancellationToken);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResult>(responseJson, JsonSerializerSettings);
        }
    }
}