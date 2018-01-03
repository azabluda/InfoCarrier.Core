// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrierSample
{
    using System;
    using System.Net.Http;
    using System.Security.Authentication;
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
        private readonly HttpClient client;
        private string transactionId;

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings().ConfigureRemoteLinq();

        public WebApiBackendImpl()
        {
            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
            };

            this.client = new HttpClient(handler, true) { BaseAddress = new Uri(WebApiShared.BaseAddress) };
        }

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
            this.CallApiAsync("CommitTransaction", null, default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
            this.transactionId = null;
        }

        public void RollbackTransaction()
        {
            this.CallApiAsync("RollbackTransaction", null, default).Wait(); // Watchout: https://stackoverflow.com/a/35831540
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