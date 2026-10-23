// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Deployment;

public class InfoCarrierDeploymentBuilderTest
{
    [Fact]
    public void Null_arrays_and_items_fail_during_configuration()
    {
        var builder = new InfoCarrierDeploymentBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddAnonymousShapes(null!));
        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new object[] { null! }));
        Assert.Throws<ArgumentNullException>(() => builder.AddAllowedTypes(null!));
        Assert.Throws<ArgumentException>(() => builder.AddAllowedTypes([typeof(DateOnly), null!]));
        Assert.Throws<ArgumentNullException>(() => builder.AddValueMappers(null!));
        Assert.Throws<ArgumentException>(() => builder.AddValueMappers([new StubMapper(), null!]));
    }

    [Fact]
    public void Anonymous_prototypes_accept_nested_anonymous_types_and_reject_named_types()
    {
        var builder = new InfoCarrierDeploymentBuilder();

        builder.AddAnonymousShapes(new { Item = new { Id = 0 } });

        Assert.Throws<ArgumentException>(() => builder.AddAnonymousShapes(new NamedRecord(1)));
    }

    [Fact]
    public void Shared_configuration_is_fluent_and_boolean_permissions_can_be_reset()
    {
        var builder = new InfoCarrierDeploymentBuilder();
        var mapper = new StubMapper();

        InfoCarrierDeploymentBuilder result = builder
            .AddAnonymousShapes(new { Id = 0 })
            .AddAllowedTypes(typeof(DateOnly))
            .AddValueMappers(mapper)
            .AllowArbitrarySqlExecution(true)
            .AllowArbitrarySqlExecution(false);
        var services = new ServiceCollection();
        services.AddInfoCarrier(configure => configure
            .AllowArbitrarySqlExecution(true)
            .AllowArbitrarySqlExecution(false));

        Assert.Same(builder, result);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IInfoCarrierArbitrarySqlExecution>());
    }

    [Fact]
    public void Client_configuration_exposes_the_fluent_boolean_permission()
    {
        var options = new DbContextOptionsBuilder();

        options.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AllowArbitrarySqlExecution(true));

        Assert.True(options.Options.FindExtension<InfoCarrierOptionsExtension>()!.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Obsolete_AllowTypes_matches_shared_builder()
    {
        var compatibilityOptions = new DbContextOptionsBuilder();
        var sharedOptions = new DbContextOptionsBuilder();

#pragma warning disable ICC1001 // The test protects the obsolete version 10 compatibility API.
        compatibilityOptions.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AllowTypes(typeof(DateOnly)));
#pragma warning restore ICC1001
        sharedOptions.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AddAllowedTypes(typeof(DateOnly)));

        Assert.Equal(
            sharedOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!.AllowedTypes,
            compatibilityOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!.AllowedTypes);
    }

    [Fact]
    public void Obsolete_parameterless_AllowArbitrarySqlExecution_matches_shared_builder()
    {
        var compatibilityOptions = new DbContextOptionsBuilder();
        var sharedOptions = new DbContextOptionsBuilder();

#pragma warning disable ICC1002 // The test protects the obsolete version 10 compatibility API.
        compatibilityOptions.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AllowArbitrarySqlExecution());
#pragma warning restore ICC1002
        sharedOptions.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AllowArbitrarySqlExecution(true));

        Assert.Equal(
            sharedOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!.ArbitrarySqlExecutionAllowed,
            compatibilityOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Abstractions_assembly_exposes_only_the_two_approved_contracts()
        => Assert.Equal(
            [
                "InfoCarrier.Core.InfoCarrierDeploymentBuilder",
                "InfoCarrier.Core.ValueMapping.IInfoCarrierValueMapper"
            ],
            typeof(InfoCarrierDeploymentBuilder).Assembly
                .GetExportedTypes()
                .Select(type => type.FullName)
                .Order());

    [Fact]
    public void Abstractions_assembly_references_only_framework_assemblies()
        => Assert.DoesNotContain(
            typeof(InfoCarrierDeploymentBuilder).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is not null
                && (reference.Name.StartsWith("InfoCarrier", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)));

    [Fact]
    public void Existing_UseInfoCarrier_callback_accepts_shared_extension()
    {
        var options = new DbContextOptionsBuilder();

        options.UseInfoCarrier(StubClient.Instance, builder => builder.ConfigureInfoCarrierForTest());

        InfoCarrierOptionsExtension extension = options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.Equal(typeof(DateOnly), Assert.Single(extension.AllowedTypes));
        Assert.True(extension.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Client_specific_calls_chain_before_shared_extension()
    {
        var options = new DbContextOptionsBuilder();

        options.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.UseNonRelationalServerStore().ConfigureInfoCarrierForTest());

        InfoCarrierOptionsExtension extension = options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.False(extension.ServerStoreIsRelational);
        Assert.Equal(typeof(DateOnly), Assert.Single(extension.AllowedTypes));
    }

    [Fact]
    public void Client_callback_invokes_shared_configuration_once()
    {
        var options = new DbContextOptionsBuilder();
        var calls = 0;

        options.UseInfoCarrier(
            StubClient.Instance,
            builder =>
            {
                calls++;
                builder.AddAllowedTypes(typeof(DateOnly));
            });

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Different_mapper_instances_do_not_share_an_internal_provider()
    {
        InfoCarrierOptionsExtension first = ClientExtension(new StubMapper());
        InfoCarrierOptionsExtension second = ClientExtension(new StubMapper());

        Assert.NotEqual(0, first.Info.GetServiceProviderHashCode());
        Assert.NotEqual(0, second.Info.GetServiceProviderHashCode());
        Assert.False(first.Info.ShouldUseSameServiceProvider(second.Info));
    }

    [Fact]
    public void No_application_mapper_keeps_zero_service_provider_hash()
    {
        InfoCarrierOptionsExtension first = ClientExtension();
        InfoCarrierOptionsExtension second = ClientExtension();

        Assert.Equal(0, first.Info.GetServiceProviderHashCode());
        Assert.Equal(0, second.Info.GetServiceProviderHashCode());
        Assert.True(first.Info.ShouldUseSameServiceProvider(second.Info));
    }

    [Fact]
    public void Application_mappers_run_before_standard_and_model_mappers()
    {
        var mapper = new StubMapper();
        InfoCarrierOptionsExtension extension = ClientExtension(mapper);
        var services = new ServiceCollection();

        extension.ApplyServices(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        IInfoCarrierValueMapper[] mappers = provider.GetServices<IInfoCarrierValueMapper>().ToArray();
        Assert.Same(mapper, mappers[0]);
        Assert.IsType<IPAddressValueMapper>(mappers[1]);
        Assert.IsType<UriValueMapper>(mappers[2]);
    }

    [Fact]
    public void Server_AddInfoCarrier_translates_shared_permissions()
    {
        var services = new ServiceCollection();

        services.AddInfoCarrier(builder => builder
            .AddAllowedTypes(typeof(DateOnly))
            .AllowArbitrarySqlExecution(true));

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(
            typeof(DateOnly),
            Assert.Single(provider.GetServices<global::InfoCarrier.Core.Expressions.IInfoCarrierAllowedTypes>()).Types.Single());
        Assert.NotNull(provider.GetService<IInfoCarrierArbitrarySqlExecution>());
    }

    [Fact]
    public void Server_AddInfoCarrier_invokes_configuration_once_and_rejects_null()
    {
        var services = new ServiceCollection();
        var calls = 0;

        services.AddInfoCarrier(_ => calls++);

        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => services.AddInfoCarrier(null!));
        Assert.Throws<ArgumentNullException>(() =>
            InfoCarrierServiceCollectionExtensions.AddInfoCarrier(null!, _ => { }));
    }

    [Fact]
    public void Repeated_server_calls_compose_application_mappers_before_standard_mappers()
    {
        var first = new StubMapper();
        var second = new StubMapper();
        var services = new ServiceCollection();

        services.AddInfoCarrier(builder => builder.AddValueMappers(first));
        services.AddInfoCarrier(builder => builder.AddValueMappers(second));

        using ServiceProvider provider = services.BuildServiceProvider();
        IInfoCarrierValueMapper[] mappers = provider.GetServices<IInfoCarrierValueMapper>().ToArray();
        Assert.Equal(4, mappers.Length);
        Assert.Same(first, mappers[0]);
        Assert.Same(second, mappers[1]);
        Assert.IsType<IPAddressValueMapper>(mappers[2]);
        Assert.IsType<UriValueMapper>(mappers[3]);
    }

    [Fact]
    public void Application_mapper_lists_start_empty_on_both_roles()
    {
        InfoCarrierOptionsExtension client = ClientExtension();
        var clientServices = new ServiceCollection();
        client.ApplyServices(clientServices);
        var services = new ServiceCollection();

        services.AddInfoCarrier();

        using ServiceProvider provider = services.BuildServiceProvider();
        using ServiceProvider clientProvider = clientServices.BuildServiceProvider();
        Assert.Collection(
            clientProvider.GetServices<IInfoCarrierValueMapper>(),
            mapper => Assert.IsType<IPAddressValueMapper>(mapper),
            mapper => Assert.IsType<UriValueMapper>(mapper));
        Assert.Collection(
            provider.GetServices<IInfoCarrierValueMapper>(),
            mapper => Assert.IsType<IPAddressValueMapper>(mapper),
            mapper => Assert.IsType<UriValueMapper>(mapper));
    }

    [Fact]
    public void One_application_extension_configures_both_roles_without_shared_state()
    {
        var created = new List<StubMapper>();
        InfoCarrierDeploymentBuilder Configure(InfoCarrierDeploymentBuilder builder)
        {
            var mapper = new StubMapper();
            created.Add(mapper);
            builder.AddValueMappers(mapper);
            return builder;
        }

        var options = new DbContextOptionsBuilder();
        options.UseInfoCarrier(StubClient.Instance, builder => Configure(builder));
        InfoCarrierOptionsExtension client = options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        var clientServices = new ServiceCollection();
        client.ApplyServices(clientServices);
        var services = new ServiceCollection();
        services.AddInfoCarrier(builder => Configure(builder));

        using ServiceProvider clientProvider = clientServices.BuildServiceProvider();
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Equal(2, created.Count);
        Assert.NotSame(created[0], created[1]);
        Assert.Same(created[0], clientProvider.GetServices<IInfoCarrierValueMapper>().First());
        Assert.Same(created[1], provider.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Failed_additive_server_call_does_not_change_existing_state()
    {
        var accepted = new StubMapper();
        var rejected = new StubMapper();
        var services = new ServiceCollection();
        services.AddInfoCarrier(builder => builder.AddValueMappers(accepted));

        Assert.Throws<ArgumentException>(() => services.AddInfoCarrier(builder =>
        {
            builder.AddValueMappers(rejected);
            builder.AddAllowedTypes([typeof(DateOnly), null!]);
        }));

        using ServiceProvider provider = services.BuildServiceProvider();
        IInfoCarrierValueMapper[] mappers = provider.GetServices<IInfoCarrierValueMapper>().ToArray();
        Assert.DoesNotContain(rejected, mappers);
        Assert.Same(accepted, mappers[0]);
    }

    [Fact]
    public void Server_configuration_snapshots_caller_owned_arrays()
    {
        var original = new StubMapper();
        IInfoCarrierValueMapper[] mappers = [original];
        var services = new ServiceCollection();
        services.AddInfoCarrier(builder => builder.AddValueMappers(mappers));

        mappers[0] = new StubMapper();

        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(original, provider.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Version_10_3_marks_only_approved_v11_configuration_removals_obsolete()
    {
        const string url = "https://github.com/azabluda/InfoCarrier.Core/issues/221";
        (MethodInfo Method, string DiagnosticId, string Replacement)[] expected =
        [
            (Method(typeof(InfoCarrierDbContextOptionsBuilder), nameof(InfoCarrierDbContextOptionsBuilder.AllowTypes), 1),
                "ICC1001", "InfoCarrierDeploymentBuilder.AddAllowedTypes"),
            (Method(typeof(InfoCarrierDbContextOptionsBuilder), nameof(InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution), 0),
                "ICC1002", "InfoCarrierDeploymentBuilder.AllowArbitrarySqlExecution"),
            (Method(typeof(InfoCarrierServiceCollectionExtensions), nameof(InfoCarrierServiceCollectionExtensions.AddInfoCarrierAllowedTypes), 2),
                "ICC1003", "InfoCarrierDeploymentBuilder.AddAllowedTypes"),
            (Method(typeof(InfoCarrierServiceCollectionExtensions), nameof(InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution), 1),
                "ICC1004", "InfoCarrierDeploymentBuilder.AllowArbitrarySqlExecution"),
            (Method(typeof(InfoCarrierServiceCollectionExtensions), nameof(InfoCarrierServiceCollectionExtensions.AddInfoCarrierStandardValueMappers), 1),
                "ICC1005", "AddInfoCarrier")
        ];

        foreach ((MethodInfo method, string diagnosticId, string replacement) in expected)
        {
            ObsoleteAttribute obsolete = Assert.Single(method.GetCustomAttributes<ObsoleteAttribute>());
            Assert.False(obsolete.IsError);
            Assert.Equal(diagnosticId, obsolete.DiagnosticId);
            Assert.Equal(url, obsolete.UrlFormat);
            Assert.Contains(replacement, obsolete.Message, StringComparison.Ordinal);
        }

        MethodInfo[] obsoleteConfigurationMethods =
        [
            .. typeof(InfoCarrierDbContextOptionsBuilder).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            .. typeof(InfoCarrierServiceCollectionExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
        ];
        Assert.Equal(
            expected.Select(item => item.Method).OrderBy(method => method.Name),
            obsoleteConfigurationMethods
                .Where(method => method.IsDefined(typeof(ObsoleteAttribute)))
                .OrderBy(method => method.Name));
    }

    [Fact]
    public void Unreleased_anonymous_role_methods_are_absent()
    {
        Assert.DoesNotContain(
            typeof(InfoCarrierDbContextOptionsBuilder).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.Name is "UseInfoCarrierDeployment" or "UseRegisteredAnonymousShapes");
        Assert.DoesNotContain(
            typeof(InfoCarrierServiceCollectionExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public),
            method => method.Name == "AddInfoCarrierDeployment"
                || method.Name == "AddInfoCarrierAnonymousShapes");
    }

    private static InfoCarrierOptionsExtension ClientExtension(params IInfoCarrierValueMapper[] mappers)
    {
        var options = new DbContextOptionsBuilder();
        options.UseInfoCarrier(
            StubClient.Instance,
            builder => builder.AddValueMappers(mappers));
        return options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
    }

    private static MethodInfo Method(Type type, string name, int parameterCount)
        => Assert.Single(
            type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public),
            method => method.Name == name && method.GetParameters().Length == parameterCount);

    private sealed record NamedRecord(int Value);

    private sealed class StubMapper : IInfoCarrierValueMapper
    {
        public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
        {
            wireValue = null;
            return false;
        }

        public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
        {
            value = null;
            return false;
        }
    }

    private sealed class StubClient : IInfoCarrierClient
    {
        public static StubClient Instance { get; } = new();

        public string ServerUrl => nameof(InfoCarrierDeploymentBuilderTest);

        public Task<QueryDataResult> QueryDataAsync(
            QueryDataRequest request,
            DbContext clientContext,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SaveChangesResult> SaveChangesAsync(
            SaveChangesRequest request,
            DbContext clientContext,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<TransactionResult> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CommitTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RollbackTransactionAsync(string transactionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CreateSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RollbackToSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ReleaseSavepointAsync(
            string transactionId,
            string name,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SupportsSavepointsAsync(
            string transactionId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public QueryDataResult QueryData(QueryDataRequest request, DbContext clientContext)
        {
            throw new NotImplementedException();
        }

        public SaveChangesResult SaveChanges(SaveChangesRequest request, DbContext clientContext)
        {
            throw new NotImplementedException();
        }

        public TransactionResult BeginTransaction()
        {
            throw new NotImplementedException();
        }

        public void CommitTransaction(string transactionId)
        {
            throw new NotImplementedException();
        }

        public void RollbackTransaction(string transactionId)
        {
            throw new NotImplementedException();
        }

        public void CreateSavepoint(string transactionId, string name)
        {
            throw new NotImplementedException();
        }

        public void RollbackToSavepoint(string transactionId, string name)
        {
            throw new NotImplementedException();
        }

        public void ReleaseSavepoint(string transactionId, string name)
        {
            throw new NotImplementedException();
        }

        public bool SupportsSavepoints(string transactionId)
        {
            throw new NotImplementedException();
        }
    }
}

internal static class InfoCarrierDeploymentBuilderTestExtensions
{
    public static InfoCarrierDeploymentBuilder ConfigureInfoCarrierForTest(
        this InfoCarrierDeploymentBuilder builder)
        => builder
            .AddAllowedTypes(typeof(DateOnly))
            .AllowArbitrarySqlExecution(true);
}
