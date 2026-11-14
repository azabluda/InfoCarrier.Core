// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Associations;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Associations;

public class CollectionTest(CollectionFixture fixture) : CollectionTests<CollectionFixture>(fixture);
public class MiscellaneousTest(MiscellaneousFixture fixture) : OwnedNavigationsMiscellaneousTestBase<MiscellaneousFixture>(fixture);
public class PrimitiveCollectionTest(PrimitiveCollectionFixture fixture) : PrimitiveCollectionTests<PrimitiveCollectionFixture>(fixture);
public class ProjectionTest(ProjectionFixture fixture) : ProjectionTests<ProjectionFixture>(fixture);
public class SetOperationsTest(SetOperationsFixture fixture) : SetOperationsTests<SetOperationsFixture>(fixture);
public class StructuralEqualityTest(StructuralEqualityFixture fixture) : StructuralEqualityTests<StructuralEqualityFixture>(fixture);
public class DirectCollectionTest(DirectCollectionFixture fixture) : CollectionTests<DirectCollectionFixture>(fixture);
public class DirectMiscellaneousTest(DirectMiscellaneousFixture fixture) : OwnedNavigationsMiscellaneousTestBase<DirectMiscellaneousFixture>(fixture);
public class DirectPrimitiveCollectionTest(DirectPrimitiveCollectionFixture fixture) : PrimitiveCollectionTests<DirectPrimitiveCollectionFixture>(fixture);
public class DirectProjectionTest(DirectProjectionFixture fixture) : ProjectionTests<DirectProjectionFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Associations/OwnedNavigations/OwnedNavigationsProjectionCosmosTest.cs";
    [StoreLimit(UpstreamRepository.EfCore, Source, 441, 447, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.QueryWrittenOut, DeviationNote = "Exercise both tracking modes; upstream omits the tracked arm. Avoid the common base's assertion of a different tracking error.")]
    public override Task Select_subquery_required_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => AssertQuery(ss => ss.Set<RootEntity>().Select(x => ss.Set<RootEntity>()
            .OrderBy(e => e.Id).Select(e => e.RequiredAssociate).FirstOrDefault()!.RequiredNestedAssociate),
            queryTrackingBehavior: queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 449, 455, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.QueryWrittenOut, DeviationNote = "Exercise both tracking modes; upstream omits the tracked arm. Avoid the common base's assertion of a different tracking error.")]
    public override Task Select_subquery_optional_related_FirstOrDefault(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => AssertQuery(ss => ss.Set<RootEntity>().Select(x => ss.Set<RootEntity>()
            .OrderBy(e => e.Id).Select(e => e.OptionalAssociate).FirstOrDefault()!.RequiredNestedAssociate),
            queryTrackingBehavior: queryTrackingBehavior));
    [StoreLimit(UpstreamRepository.EfCore, Source, 429, 439, Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.QueryWrittenOut, DeviationNote = "Exercise both tracking modes; upstream omits the tracked arm. Avoid the common base's assertion of a different tracking error.")]
    public override Task Select_subquery_FirstOrDefault_complex_collection(QueryTrackingBehavior queryTrackingBehavior)
        => AssertTranslationFailed(() => AssertQuery(ss => ss.Set<RootEntity>().OrderBy(x => x.Id).Select(x => ss.Set<RootEntity>()
            .OrderBy(e => e.Id).FirstOrDefault()!.AssociateCollection),
            assertOrder: true, elementAsserter: (e, a) => AssertCollection(e, a, elementSorter: r => r.Id),
            queryTrackingBehavior: queryTrackingBehavior));
}
public class DirectSetOperationsTest(DirectSetOperationsFixture fixture) : SetOperationsTests<DirectSetOperationsFixture>(fixture);
public class DirectStructuralEqualityTest(DirectStructuralEqualityFixture fixture) : StructuralEqualityTests<DirectStructuralEqualityFixture>(fixture);

public class CollectionFixture : DocumentAssociationFixture;
public class MiscellaneousFixture : DocumentAssociationFixture;
public class PrimitiveCollectionFixture : DocumentAssociationFixture;
public class ProjectionFixture : DocumentAssociationFixture;
public class SetOperationsFixture : DocumentAssociationFixture;
public class StructuralEqualityFixture : DocumentAssociationFixture;
public class DirectCollectionFixture : DirectAssociationFixture;
public class DirectMiscellaneousFixture : DirectAssociationFixture;
public class DirectPrimitiveCollectionFixture : DirectAssociationFixture;
public class DirectProjectionFixture : DirectAssociationFixture;
public class DirectSetOperationsFixture : DirectAssociationFixture;
public class DirectStructuralEqualityFixture : DirectAssociationFixture;

public abstract class DirectAssociationFixture : DocumentAssociationFixture
{
    private ITestStoreFactory? _directFactory;
    protected override ITestStoreFactory TestStoreFactory => _directFactory ??= new CosmosDirectFactory(OnModelCreating);
    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder).ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined));
}

internal sealed class CosmosDirectFactory(Action<ModelBuilder, DbContext> onModelCreating) : ITestStoreFactory
{
    private SharedTestStoreProperties Properties() => new()
    {
        ContextType = typeof(PoolableDbContext), OnModelCreating = onModelCreating,
    };
    public TestStore Create(string name) => new CosmosBackendStore(name, false, Properties());
    public TestStore GetOrCreate(string name) => new CosmosBackendStore(name, true, Properties());
    public IServiceCollection AddProviderServices(IServiceCollection services)
        => services.AddEntityFrameworkCosmos().AddSingleton<TestStoreIndex>();
    public ListLoggerFactory CreateListLoggerFactory(Func<string, bool> filter) => new(filter);
}

public abstract class DocumentAssociationFixture : OwnedNavigationsFixtureBase
{
    private ITestStoreFactory? _factory;
    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), typeof(PoolableDbContext), (model, context) => OnModelCreating(model, context),
        configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        // Match EF Core 11's Cosmos owned-navigation fixture, including its single document root.
        modelBuilder.Ignore<RootReferencingEntity>();
        modelBuilder.Entity<RootEntity>().ToContainer("RootEntities").HasNoDiscriminator();
    }
}

internal sealed class CosmosTier : InfoCarrierTier
{
    public override InfoCarrierBackendTestStore CreateBackend(string name, bool shared, SharedTestStoreProperties properties)
        => new CosmosBackendStore(name, shared, properties);
}

internal sealed class CosmosBackendStore(string name, bool shared, SharedTestStoreProperties properties)
    : InfoCarrierBackendTestStore(name, shared, properties)
{
    private readonly string _database = "icc-assoc-" + Guid.NewGuid().ToString("N");
    public override bool ServerStoreIsRelational => false;
    protected override IServiceCollection AddServices(IServiceCollection services)
        => services.AddEntityFrameworkCosmos().AddSingleton<TestStoreIndex>();
    protected override TestStoreIndex GetTestStoreIndex(IServiceProvider? provider)
        => provider?.GetService<TestStoreIndex>() ?? base.GetTestStoreIndex(provider);
    public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
        => CosmosEmulator.Configure(base.AddProviderOptions(builder), _database)
            .ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined));
    public override async Task CleanAsync(DbContext context, bool createTables = true)
    {
        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        if (createTables) await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }
    public override async ValueTask DisposeAsync()
    {
        using Microsoft.Azure.Cosmos.CosmosClient client = CosmosEmulator.Client();
        try
        {
            await client.GetDatabase(_database).DeleteAsync();
        }
        catch (Microsoft.Azure.Cosmos.CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound) { }
        await base.DisposeAsync();
    }
}
