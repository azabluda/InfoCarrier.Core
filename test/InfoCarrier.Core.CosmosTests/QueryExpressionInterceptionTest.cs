// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

public class QueryExpressionInterceptionTest(QueryExpressionInterceptionTest.CosmosFixture fixture)
    : QueryExpressionInterceptionTestBase(fixture), IClassFixture<QueryExpressionInterceptionTest.CosmosFixture>
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/QueryExpressionInterceptionWithDiagnosticsCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 11, 12, Justification = Upstream.GaveNoReason)]
    public override Task Intercept_query_passively(bool async)
        => NoSyncTest(async, a => base.Intercept_query_passively(a));

    [StoreLimit(UpstreamRepository.EfCore, Source, 14, 15, Justification = Upstream.GaveNoReason)]
    public override Task Intercept_query_with_multiple_interceptors(bool async)
        => NoSyncTest(async, a => base.Intercept_query_with_multiple_interceptors(a));

    [StoreLimit(UpstreamRepository.EfCore, Source, 17, 18, Justification = Upstream.GaveNoReason)]
    public override Task Intercept_to_change_query_expression(bool async)
        => NoSyncTest(async, a => base.Intercept_to_change_query_expression(a));

    [StoreLimit(UpstreamRepository.EfCore, Source, 20, 21, Justification = Upstream.GaveNoReason)]
    public override Task Interceptor_does_not_leak_across_contexts(bool async)
        => NoSyncTest(async, a => base.Interceptor_does_not_leak_across_contexts(a));

    private static async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async) await test(true);
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }

    public class CosmosFixture : InterceptionFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection services, IEnumerable<IInterceptor> interceptors)
            => base.InjectInterceptors(TestStoreFactory.AddProviderServices(services), interceptors);
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.HasDiscriminatorInJsonIds();
        }
        protected override string StoreName => "CosmosQueryExpressionInterception";
        protected override bool ShouldSubscribeToDiagnosticListener => true;
    }
}
