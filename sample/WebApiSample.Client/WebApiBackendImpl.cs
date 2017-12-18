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
        private string transactionId;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();

        public string ServerUrl
            => this.client.BaseAddress.ToString();

        public void BeginTransaction()
            => this.BeginTransactionAsync(default).Wait(); // Watchout: https://stackoverflow.com/a/35831540

        public async Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            this.transactionId = await this.CallApiAsync<string>("BeginTransaction", null, cancellationToken);
        }

        public void CommitTransaction()
        {
            this.CallApiAsync<object>("CommitTransaction", null, default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
            this.transactionId = null;
        }

        public void RollbackTransaction()
        {
            this.CallApiAsync<object>("RollbackTransaction", null, default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
            this.transactionId = null;
        }

        public QueryDataResult QueryData(QueryDataRequest request, DbContext dbContext)
            => this.QueryDataAsync(request, dbContext, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<QueryDataResult> QueryDataAsync(QueryDataRequest request, DbContext dbContext, CancellationToken cancellationToken)
            => await this.CallApiAsync<QueryDataResult>("QueryData", request, cancellationToken);

        public SaveChangesResult SaveChanges(SaveChangesRequest request)
            => this.SaveChangesAsync(request, default).Result; // Watchout: https://stackoverflow.com/a/35831540

        public async Task<SaveChangesResult> SaveChangesAsync(SaveChangesRequest request, CancellationToken cancellationToken)
            => await this.CallApiAsync<SaveChangesResult>("SaveChanges", request, cancellationToken);

        private async Task<TResult> CallApiAsync<TResult>(string action, object request, CancellationToken cancellationToken)
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

            switch (response.Content.Headers.ContentType?.MediaType)
            {
                case null:
                    return default;

                case "application/json":
                    string responseJson = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResult>(responseJson, JsonSerializerSettings);

                case System.Net.Mime.MediaTypeNames.Text.Plain:
                    return (TResult)(object)await response.Content.ReadAsStringAsync();

                default:
                    throw new NotSupportedException(response.Content.Headers.ContentType.ToString());
            }
        }
    }
}