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
        var builder = new InfoCarrierClientOptions();
        InfoCarrierClientOptions result = builder
            .AddAnonymousShapes(new { Id = 0 })
            .AddAllowedTypes(typeof(DateOnly))
            .AddValueMappers(new StubMapper())
            .AllowArbitrarySqlExecution(true)
            .AllowArbitrarySqlExecution(false);
        Assert.Same(builder, result);
        var options = new DbContextOptionsBuilder();
        options.UseInfoCarrier(StubClient.Instance, o => o.ConfigureShared(b => b
            .AllowArbitrarySqlExecution(true).AllowArbitrarySqlExecution(false)));
        Assert.False(options.Options.FindExtension<InfoCarrierOptionsExtension>()!.ArbitrarySqlExecutionAllowed);
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
    public void Abstractions_assembly_exposes_only_the_shared_configuration_surface()
        => Assert.Equal(
            [
                "InfoCarrier.Core.InfoCarrierDeploymentBuilder",
                "InfoCarrier.Core.InfoCarrierDeploymentBuilderExtensions",
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
    public void Existing_UseInfoCarrier_callback_accepts_shared_configuration()
    {
        var options = new DbContextOptionsBuilder();

        options.UseInfoCarrier(StubClient.Instance, builder => builder.ConfigureShared(
            InfoCarrierDeploymentBuilderTestExtensions.ConfigureInfoCarrierForTest));

        InfoCarrierOptionsExtension extension = options.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.Equal(typeof(DateOnly), Assert.Single(extension.AllowedTypes));
        Assert.True(extension.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Client_specific_calls_compose_with_shared_configuration()
    {
        var options = new DbContextOptionsBuilder();

        options.UseInfoCarrier(
            StubClient.Instance,
            builder =>
            {
                builder.ServerUsesRelationalQueryRules = false;
                builder.ConfigureShared(InfoCarrierDeploymentBuilderTestExtensions.ConfigureInfoCarrierForTest);
            });

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
    public void Server_registration_invokes_configuration_once_and_rejects_null_services()
    {
        var services = new ServiceCollection();
        var calls = 0;
        services.AddInfoCarrierServer<DbContext>(_ => calls++);
        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() =>
            InfoCarrierRoleServiceCollectionExtensions.AddInfoCarrierServer<DbContext>(null!));
    }





    [Fact]
    public void One_application_method_configures_both_roles_without_shared_state()
    {
        var created = new List<StubMapper>();
        void Configure(InfoCarrierDeploymentBuilder builder)
        {
            var mapper = new StubMapper();
            created.Add(mapper);
            builder.AddValueMappers(mapper);
        }
        using var http = new HttpClient();
        IInfoCarrierClient client = InfoCarrierClient.Create(http, o => o
            .WithEndpoint("https://example.test/data").ConfigureShared(Configure));
        var services = new ServiceCollection();
        services.AddInfoCarrierServer<DbContext>(o => o.ConfigureShared(Configure));
        Assert.Equal(2, created.Count);
        Assert.NotSame(created[0], created[1]);
        var options = new DbContextOptionsBuilder().UseInfoCarrier(client);
        var clientServices = new ServiceCollection();
        options.Options.FindExtension<InfoCarrierOptionsExtension>()!.ApplyServices(clientServices);
        using ServiceProvider provider = clientServices.BuildServiceProvider();
        Assert.Same(created[0], provider.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Failed_server_registration_does_not_change_existing_services()
    {
        var services = new ServiceCollection();
        services.AddInfoCarrierServer<DbContext>(o => o.AddValueMappers(new StubMapper()));
        ServiceDescriptor[] before = [.. services];
        Assert.Throws<ArgumentException>(() => services.AddInfoCarrierServer<DbContext>(o => o
            .AddValueMappers(new StubMapper()).AddAllowedTypes([typeof(DateOnly), null!])));
        Assert.Equal(before, services);
    }

    [Fact]
    public void Client_configuration_snapshots_caller_owned_mapper_arrays()
    {
        var original = new StubMapper();
        IInfoCarrierValueMapper[] mappers = [original];
        InfoCarrierOptionsExtension client = ClientExtension(mappers);
        mappers[0] = new StubMapper();
        var services = new ServiceCollection();
        client.ApplyServices(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        Assert.Same(original, provider.GetServices<IInfoCarrierValueMapper>().First());
    }

    [Fact]
    public void Version_11_removes_legacy_configuration_and_type_forwarding()
    {
        Assembly assembly = typeof(InfoCarrierClientOptions).Assembly;
        foreach (string name in new[]
        {
            "InfoCarrier.Core.InfoCarrierDbContextOptionsBuilder",
            "InfoCarrier.Core.IInfoCarrierArbitrarySqlExecution",
            "InfoCarrier.Core.Expressions.IInfoCarrierAllowedTypes",
            "InfoCarrier.Core.IInfoCarrierServerDocumentStore",
            "InfoCarrier.Core.IInfoCarrierServerLogForwarding",
            "InfoCarrier.Core.IInfoCarrierSensitiveServerLogForwarding",
            "InfoCarrier.Core.IInfoCarrierServerTransactionTimeout",
        })
        {
            Assert.Null(assembly.GetType(name));
        }
        Assert.Equal(
            [nameof(InfoCarrierServiceCollectionExtensions.AddEntityFrameworkInfoCarrier)],
            typeof(InfoCarrierServiceCollectionExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Select(method => method.Name).Order());
        Assert.DoesNotContain(typeof(IInfoCarrierValueMapper), assembly.GetForwardedTypes());
    }

    [Fact]
    public void Unreleased_anonymous_role_methods_are_absent()
    {
        Assert.DoesNotContain(
            typeof(InfoCarrierClientOptions).GetMethods(BindingFlags.Instance | BindingFlags.Public),
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
    public static void ConfigureInfoCarrierForTest(InfoCarrierDeploymentBuilder builder)
        => builder
            .AddAllowedTypes(typeof(DateOnly))
            .AllowArbitrarySqlExecution(true);
}
