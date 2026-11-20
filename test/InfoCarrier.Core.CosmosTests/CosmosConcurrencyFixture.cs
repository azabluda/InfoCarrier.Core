// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

// Official F1CosmosFixture<byte[]> mapping at the pinned EF Core 11 source commit.
public class CosmosConcurrencyFixture : F1FixtureBase<byte[]>
{
    private ITestStoreFactory? _factory;
    private ListLoggerFactory? _loggerFactory;

    protected override IServiceCollection AddServices(IServiceCollection services)
        => base.AddServices(services).Replace(ServiceDescriptor.Singleton<ILoggerFactory>(
            _loggerFactory ??= new ListLoggerFactory(ShouldLogCategory)));

    public override async Task ReseedAsync()
    {
        // The spec base needs an initialized fixture in its constructor. Keep that fixture and
        // its logger, but replace its store/provider/client before each case instead of deleting
        // and recreating containers through a client which still caches their old metadata.
        // The externally supplied logger instance is not disposed by either service provider.
        await base.DisposeAsync();
        await base.InitializeAsync();
    }

    protected override bool UsePooling => false;
    public override TestHelpers TestHelpers => InfoCarrierTestHelpers.Instance;
    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), ContextType, null,
        onAddOptions: ServerOptions,
        onAddServices: services => services.AddSingleton<ISingletonInterceptor, ServerSideF1MaterializationInterceptor>(),
        configureConventions: ConfigureConventions);

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));

    private DbContextOptionsBuilder ServerOptions(DbContextOptionsBuilder builder)
        => builder.EnableSensitiveDataLogging()
            .ConfigureWarnings(w => w.Default(WarningBehavior.Throw)
                .Log(CoreEventId.SensitiveDataLoggingEnabledWarning, CoreEventId.PossibleUnintendedReferenceComparisonWarning)
                .Ignore(CosmosEventId.NoPartitionKeyDefined, CoreEventId.SaveChangesStarting, CoreEventId.SaveChangesCompleted))
            .UseModel(BuildServerModel())
            .UseAsyncSeeding(async (context, _, token) =>
            {
                if (await ShouldSeedAsync((F1Context)context))
                {
                    F1Context.AddSeedData((F1Context)context);
                    await context.SaveChangesAsync(token);
                }
            });

    protected override void BuildModelExternal(ModelBuilder modelBuilder)
    {
        base.BuildModelExternal(modelBuilder);
        modelBuilder.HasDiscriminatorInJsonIds();
        modelBuilder.Entity<Engine>(b =>
        {
            b.Property(e => e.EngineSupplierId).IsConcurrencyToken(false);
            b.Property(e => e.Name).IsConcurrencyToken(false);
            b.OwnsOne(e => e.StorageLocation, owned =>
            {
                owned.Property(e => e.Latitude).IsConcurrencyToken(false);
                owned.Property(e => e.Longitude).IsConcurrencyToken(false);
            });
        });
        modelBuilder.Entity<Chassis>().Property<string>("Version").IsETagConcurrency();
        modelBuilder.Entity<Driver>().Property<string>("Version").IsETagConcurrency();
        modelBuilder.Entity<Team>().Property<string>("Version").IsETagConcurrency();
        modelBuilder.Entity<Sponsor>(b =>
        {
            b.Property<string>("Version").IsETagConcurrency();
            b.Property<int?>(Sponsor.ClientTokenPropertyName).IsConcurrencyToken(false);
        });
        modelBuilder.Entity<TitleSponsor>().Ignore(e => e.Details).OwnsOne(e => e.Details, owned =>
        {
            owned.Property<string>("Version").IsETagConcurrency();
            owned.Property<int?>(Sponsor.ClientTokenPropertyName).IsConcurrencyToken(false);
        });
    }

    private IModel BuildServerModel()
    {
#pragma warning disable EF1001 // Build the external server model with the provider's own conventions.
        ModelBuilder builder = Microsoft.EntityFrameworkCore.Cosmos.Metadata.Conventions.Internal.CosmosConventionSetBuilder.CreateModelBuilder();
#pragma warning restore EF1001
        BuildModelExternal(builder);
        return (IModel)builder.Model;
    }

    public async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async) await test(true);
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }
}

