// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Client;
    using InfoCarrier.Core.Common;
    using Microsoft.EntityFrameworkCore;
    using Newtonsoft.Json;

    internal class WebApiInfoCarrierClientImpl : IInfoCarrierClient
    {
        private readonly HttpClient client;
        private string transactionId;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings().ConfigureInfoCarrier();

        public WebApiInfoCarrierClientImpl(HttpClient httpClient)
            => this.client = httpClient;

        public string ServerUrl
            => this.client.BaseAddress.ToString();

        public void BeginTransaction()
            => this.BeginTransactionAsync(default).Wait(); // Watchout: https://stackoverflow.com/a/35831540

        public async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            this.transactionId = await this.CallApiAsync("BeginTransaction", null, cancellationToken);
        }

        public void CommitTransaction()
        {
            this.CommitTransactionAsync(default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            await this.CallApiAsync("CommitTransaction", null, cancellationToken);
            this.transactionId = null;
        }

        public void RollbackTransaction()
        {
            this.RollbackTransactionAsync(default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
        {
            await this.CallApiAsync("RollbackTransaction", null, cancellationToken);
            this.transactionId = null;
        }

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
            => this.QueryDataAsync(request, dbContext, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
            => JsonConvert.DeserializeObject<QueryDataResult>(
                await this.CallApiAsync("QueryData", request, cancellationToken),
                JsonSerializerSettings);

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
            => this.SaveChangesAsync(request, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
            => JsonConvert.DeserializeObject<SaveChangesResult>(
                await this.CallApiAsync("SaveChanges", request, cancellationToken),
                JsonSerializerSettings);

        private async Task<string> CallApiAsync(string action, object request, CancellationToken cancellationToken)
        {
            string requestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
            HttpResponseMessage response = await this.client.PostAsync(
                $"api/{action}",
                new StringContent(requestJson, Encoding.UTF8, "application/json")
                {
                    Headers = { { WebApiShared.TransactionIdHeader, this.transactionId } },
                },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
    }
}
