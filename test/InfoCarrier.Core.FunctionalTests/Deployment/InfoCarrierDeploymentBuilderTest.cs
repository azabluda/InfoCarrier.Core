// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    }
}

internal static class InfoCarrierDeploymentBuilderTestExtensions
{
    public static InfoCarrierDeploymentBuilder ConfigureInfoCarrierForTest(
        this InfoCarrierDeploymentBuilder builder)
    {
        builder.AddAllowedTypes(typeof(DateOnly));
        builder.AllowArbitrarySqlExecution = true;
        return builder;
    }
}
