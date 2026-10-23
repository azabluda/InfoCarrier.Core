// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The three infrastructure bases EF states on its <em>InMemory</em> suite, adopted on ADR-009
///     <b>Tier A</b> (C0).
/// </summary>
/// <remarks>
///     None of these runs a query, so "tier" here means only which of EF's two suites states the
///     base — and for all three it is the InMemory one, the relational assembly carrying a
///     <c>*RelationalTestBase</c> variant this project does not reference. They test the provider's
///     own plumbing: what it logs about itself, what
///     <c>AddEntityFrameworkInfoCarrier()</c> registers, and whether the modelling idioms EF
///     documents actually build against it.
/// </remarks>
public class LoggingInfoCarrierTest : LoggingTestBase
{
    /// <inheritdoc />
    protected override DbContextOptionsBuilder CreateOptionsBuilder(IServiceCollection services)
        => InfoCarrierTestHelpers.Instance
            .UseProviderOptions(new DbContextOptionsBuilder())
            .UseInternalServiceProvider(
                services.AddEntityFrameworkInfoCarrier().BuildServiceProvider(validateScopes: true));

    /// <inheritdoc />
    protected override TestLogger CreateTestLogger()
        => new TestLogger<InfoCarrierLoggingDefinitions>();

    /// <inheritdoc />
    protected override string ProviderName
        => typeof(InfoCarrierOptionsExtension).Assembly.GetName().Name!;

    /// <inheritdoc />
    protected override string ProviderVersion
        => typeof(InfoCarrierOptionsExtension).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion!;

    /// <inheritdoc />
    /// <remarks>
    ///     <c>InfoCarrierOptionsExtension.ExtensionInfo.LogFragment</c>, which is what the provider
    ///     actually writes; the assertion is that the two agree.
    /// </remarks>
    protected override string DefaultOptions
        => "using InfoCarrier ";
}

/// <inheritdoc cref="LoggingInfoCarrierTest" />
public class ModelBuilding101InfoCarrierTest : ModelBuilding101TestBase
{
    /// <inheritdoc />
    protected override DbContextOptionsBuilder ConfigureContext(DbContextOptionsBuilder optionsBuilder)
        => InfoCarrierTestHelpers.Instance.UseProviderOptions(optionsBuilder);
}

/// <inheritdoc cref="LoggingInfoCarrierTest" />
public class InfoCarrierServiceCollectionExtensionsTest()
    : EntityFrameworkServiceCollectionExtensionsTestBase(InfoCarrierTestHelpers.Instance)
{
    /// <inheritdoc />
    /// <remarks>
    ///     InfoCarrier bypasses EF Core's translating query pipeline at <c>IDatabase</c>. The two
    ///     factory services used only by that pipeline are intentionally absent.
    /// </remarks>
    [InfoCarrierDesign(
        6,
        Justification = "InfoCarrier captures the expression at IDatabase. The later EF Core query translation "
            + "pipeline is therefore inapplicable.")]
    [ConditionalFact]
    public override void Required_services_are_registered_with_expected_lifetimes()
    {
        Dictionary<Type, string> inapplicableServices = new()
        {
            [typeof(IDatabaseCreator)] =
                "The InfoCarrier client has no database whose schema it could create or inspect.",
            [typeof(IQueryableMethodTranslatingExpressionVisitorFactory)] =
                "InfoCarrier captures the expression at IDatabase before EF Core query translation.",
            [typeof(IShapedQueryCompilingExpressionVisitorFactory)] =
                "InfoCarrier receives materialized server results without EF Core shaped-query compilation.",
        };
#pragma warning disable EF1001 // The inherited conformance test validates this internal EF Core service map.
        Dictionary<Type, ServiceCharacteristics> requiredServices = EntityFrameworkServicesBuilder.CoreServices
            .Where(service => !inapplicableServices.ContainsKey(service.Key))
            .ToDictionary(service => service.Key, service => service.Value);
#pragma warning restore EF1001

        LifetimeTest(requiredServices);

        var services = new ServiceCollection();
        services.AddEntityFrameworkInfoCarrier();
        foreach ((Type serviceType, string reason) in inapplicableServices)
        {
            Assert.False(
                services.Any(descriptor => descriptor.ServiceType == serviceType),
                $"{serviceType} is inapplicable: {reason}");
        }
    }

    [Fact]
    public void Supported_database_facade_dependencies_resolve_without_database_creator()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using var context = CreateContext(serviceProvider);

        IDatabaseFacadeDependencies dependencies = context.GetService<IDatabaseFacadeDependencies>();

        Assert.NotNull(dependencies.TransactionManager);
        Assert.NotNull(dependencies.ExecutionStrategy);
        Assert.NotNull(dependencies.ExecutionStrategyFactory);
        Assert.NotEmpty(dependencies.DatabaseProviders);
        Assert.NotNull(dependencies.CommandLogger);
        Assert.NotNull(dependencies.ConcurrencyDetector);
        Assert.NotNull(dependencies.CoreOptions);
        Assert.NotNull(dependencies.QueryProvider);
        Assert.NotNull(dependencies.AdHocMapper);
        Assert.NotNull(dependencies.TypeMappingSource);
    }

    [Fact]
    public async Task Schema_apis_explain_that_the_client_has_no_database()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using var context = CreateContext(serviceProvider);
        const string expected = "The InfoCarrier client has no database of its own, so 'DatabaseCreator' "
            + "has no value here. A caller reaching for it wants the SERVER's, which does not cross the wire.";

        Assert.Equal(expected, Assert.Throws<InvalidOperationException>(() => context.Database.EnsureCreated()).Message);
        Assert.Equal(expected, Assert.Throws<InvalidOperationException>(() => context.Database.EnsureDeleted()).Message);
        Assert.Equal(expected, Assert.Throws<InvalidOperationException>(() => context.Database.CanConnect()).Message);
        Assert.Equal(
            expected,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.EnsureCreatedAsync())).Message);
        Assert.Equal(
            expected,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.EnsureDeletedAsync())).Message);
        Assert.Equal(
            expected,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => context.Database.CanConnectAsync())).Message);
    }

    [Fact]
    public void Database_creator_cannot_be_resolved_directly()
    {
        using ServiceProvider serviceProvider = CreateServiceProvider();
        using var context = CreateContext(serviceProvider);

        Assert.Throws<InvalidOperationException>(() => context.GetService<IDatabaseCreator>());
    }

    private static ServiceProvider CreateServiceProvider()
        => new ServiceCollection()
            .AddEntityFrameworkInfoCarrier()
            .BuildServiceProvider(validateScopes: true);

    private static DbContext CreateContext(IServiceProvider serviceProvider)
        => new(
            InfoCarrierTestHelpers.Instance
                .UseProviderOptions(new DbContextOptionsBuilder())
                .UseInternalServiceProvider(serviceProvider)
                .Options);
}

/// <summary>
///     <c>ApiConsistencyTestBase</c> — the one base in C0's table with <b>no tier at all</b>.
/// </summary>
/// <remarks>
///     It asserts things about <c>InfoCarrier.Core.dll</c>'s own public surface — async suffixes,
///     virtual members, the <c>IReadOnly</c>/<c>IMutable</c>/<c>IConvention</c> metadata triples,
///     fluent-API return types — and never touches a store. EF ships one on InMemory and one on
///     SQLite because both are providers, not because either is a backing store; asking which tier
///     this belongs to is a category error, so it is stated here instead of assumed.
/// </remarks>
public class InfoCarrierApiConsistencyTest(InfoCarrierApiConsistencyTest.InfoCarrierApiConsistencyFixture fixture)
    : ApiConsistencyTestBase<InfoCarrierApiConsistencyTest.InfoCarrierApiConsistencyFixture>(fixture)
{
    /// <inheritdoc />
    protected override void AddServices(ServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    protected override Assembly TargetAssembly
        => typeof(InfoCarrierOptionsExtension).Assembly;

    public class InfoCarrierApiConsistencyFixture : ApiConsistencyFixtureBase
    {
        /// <inheritdoc />
        public override HashSet<Type> FluentApiTypes { get; } =
        [
            typeof(InfoCarrierServiceCollectionExtensions),
            typeof(InfoCarrierDbContextOptionsBuilderExtensions),
        ];
    }
}
