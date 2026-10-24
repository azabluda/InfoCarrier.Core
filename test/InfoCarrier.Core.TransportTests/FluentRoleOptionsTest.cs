// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.ValueMapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class FluentRoleOptionsTest
{
    [Fact]
    public void ConfigureShared_runs_once_synchronously_on_the_same_builder_in_chain_order()
    {
        var original = new InfoCarrierServerOptions();
        var first = new StubMapper();
        var second = new StubMapper();
        var calls = 0;

        InfoCarrierServerOptions result = original
            .WithLogForwarding(LogLevel.Warning)
            .WithMaxRequestBytes(1024)
            .AddValueMappers(first)
            .AllowArbitrarySqlExecution(true)
            .ConfigureShared(builder =>
            {
                Assert.Same(original, builder);
                Assert.Equal(1024, original.MaxRequestBytes);
                calls++;
                builder.AddValueMappers(second).AllowArbitrarySqlExecution(false);
                original.WithMaxRequestBytes(2048);
            })
            .WithMaxRequestBytes(4096);

        Assert.Equal(1, calls);
        Assert.Same(original, result);
        InfoCarrierServerRegistration registration = result.Build(typeof(TestContext));
        Assert.Equal(4096, registration.MaxRequestBytes);
        Assert.False(registration.AllowsArbitrarySql);
    }

    [Fact]
    public void ConfigureShared_rejects_null_arguments_without_invoking_the_callback()
    {
        InfoCarrierClientOptions missing = null!;
        var calls = 0;
        Assert.Throws<ArgumentNullException>(() => missing.ConfigureShared(_ => calls++));
        Assert.Equal(0, calls);
        Assert.Throws<ArgumentNullException>(() => new InfoCarrierServerOptions().ConfigureShared(null!));
    }

    [Fact]
    public void ConfigureShared_propagates_the_original_callback_exception()
    {
        var expected = new InvalidOperationException("Shared configuration failed.");
        var calls = 0;
        var builder = new InfoCarrierDeploymentBuilder();

        InvalidOperationException actual = Assert.Throws<InvalidOperationException>(() => builder.ConfigureShared(_ =>
        {
            calls++;
            throw expected;
        }));

        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Parameterized_ordinary_methods_compose_on_both_roles_without_changing_their_types()
    {
        InfoCarrierClientOptions client = new InfoCarrierClientOptions()
            .WithEndpoint("https://example.test/data")
            .ConfigureShared(b => FluentTestDeployment.Configure(b, allowSql: false))
            .WithMaxResponseBytes(4096);
        InfoCarrierServerOptions server = new InfoCarrierServerOptions()
            .WithMaxRequestBytes(8192)
            .ConfigureShared(b => FluentTestDeployment.Configure(b, allowSql: false))
            .WithLogForwarding(LogLevel.Warning);

        InfoCarrierClientRegistration clientRegistration = client.Build();
        InfoCarrierServerRegistration serverRegistration = server.Build(typeof(TestContext));
        Assert.Equal(4096, clientRegistration.MaxResponseBytes);
        Assert.False(serverRegistration.AllowsArbitrarySql);
        Assert.Contains(typeof(TimeOnly), serverRegistration.AllowedTypes);
        Assert.Single(serverRegistration.AnonymousShapeTypes);
    }

    [Fact]
    public void Shared_operations_and_configuration_preserve_client_type_in_any_order()
    {
        var original = new InfoCarrierClientOptions();
        InfoCarrierClientOptions result = original
            .AddAnonymousShapes(new { Label = "" })
            .WithEndpoint("https://example.test/data")
            .AddValueMappers(new StubMapper())
            .WithMaxResponseBytes(4096)
            .ConfigureShared(FluentTestDeployment.Configure)
            .WithServerUsesRelationalQueryRules(false)
            .AllowArbitrarySqlExecution(false)
            .WithMaxResponseBytes(null);

        Assert.Same(original, result);
        InfoCarrierClientRegistration registration = result.Build();
        Assert.Equal(new Uri("https://example.test/data"), registration.Endpoint);
        Assert.Null(registration.MaxResponseBytes);
        Assert.False(registration.ServerUsesRelationalQueryRules);
    }

    [Fact]
    public void Shared_operations_and_configuration_preserve_server_type_in_any_order()
    {
        var original = new InfoCarrierServerOptions();
        InfoCarrierServerOptions result = original
            .AddAllowedTypes(typeof(DateOnly))
            .WithMaxRequestBytes(8192)
            .AddValueMappers(new StubMapper())
            .WithStoresOwnedDataInsideOwner(true)
            .ConfigureShared(FluentTestDeployment.Configure)
            .WithLogForwarding(LogLevel.Warning)
            .WithTransactionIdleTimeout(TimeSpan.FromMinutes(10));

        Assert.Same(original, result);
        InfoCarrierServerRegistration registration = result.Build(typeof(TestContext));
        Assert.Equal(8192, registration.MaxRequestBytes);
        Assert.True(registration.StoresOwnedDataInsideOwner);
        Assert.True(registration.AllowsArbitrarySql);
        Assert.Contains(typeof(DateOnly), registration.AllowedTypes);
        Assert.Single(registration.AnonymousShapeTypes);
        Assert.Equal(TimeSpan.FromMinutes(10), registration.TransactionIdleTimeout);
    }

    [Fact]
    public void Reconfiguring_log_forwarding_resets_the_sensitive_grant_and_can_disable_forwarding()
    {
        var options = new InfoCarrierServerOptions()
            .WithLogForwarding(LogLevel.Information, includeSensitiveData: true);
        Assert.True(options.Build(typeof(TestContext)).IncludeSensitiveLogData);

        options.WithLogForwarding(LogLevel.Warning);
        InfoCarrierServerRegistration ordinary = options.Build(typeof(TestContext));
        Assert.Equal(LogLevel.Warning, ordinary.LogForwardingMinimumLevel);
        Assert.False(ordinary.IncludeSensitiveLogData);

        options.WithLogForwarding(LogLevel.Information, includeSensitiveData: true)
            .WithLogForwarding(null);
        InfoCarrierServerRegistration disabled = options.Build(typeof(TestContext));
        Assert.Null(disabled.LogForwardingMinimumLevel);
        Assert.False(disabled.IncludeSensitiveLogData);
    }

    [Fact]
    public void Sensitive_forwarding_still_requires_a_level()
        => Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddInfoCarrierServer<TestContext>(o => o.WithLogForwarding(null, includeSensitiveData: true)));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fluent_limits_cannot_bypass_registration_validation(int invalidLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddInfoCarrierClient(o => o
            .WithEndpoint("https://example.test/data")
            .WithMaxResponseBytes(invalidLimit)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddInfoCarrierServer<TestContext>(o => o
            .WithMaxRequestBytes(invalidLimit)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ServiceCollection().AddInfoCarrierServer<TestContext>(o => o
            .WithTransactionIdleTimeout(TimeSpan.FromSeconds(invalidLimit))));
    }

    [Fact]
    public void Relative_fluent_endpoints_are_rejected_during_registration()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfoCarrierClient(o => o
            .WithEndpoint("relative")));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddInfoCarrierClient(o => o
            .WithEndpoint(new Uri("relative", UriKind.Relative))));
    }

    [Fact]
    public void Later_fluent_changes_do_not_mutate_a_registered_client_snapshot()
    {
        InfoCarrierClientOptions? mutable = null;
        using var http = new HttpClient();
        IInfoCarrierClient client = InfoCarrierClient.Create(http, o => mutable = o
            .WithEndpoint(new Uri("https://first.test/data"))
            .WithMaxResponseBytes(1024)
            .WithServerUsesRelationalQueryRules(false)
            .ConfigureShared(FluentTestDeployment.Configure));

        mutable!.WithEndpoint("https://second.test/data")
            .WithMaxResponseBytes(null)
            .WithServerUsesRelationalQueryRules(true)
            .ConfigureShared(b => b.AddAllowedTypes(typeof(DateOnly)).AllowArbitrarySqlExecution(false));

        InfoCarrierClientRegistration registration = ((IInfoCarrierClientConfiguration)client).Registration!;
        Assert.Equal(new Uri("https://first.test/data"), registration.Endpoint);
        Assert.Equal(1024, registration.MaxResponseBytes);
        Assert.False(registration.ServerUsesRelationalQueryRules);
        var contextOptions = new DbContextOptionsBuilder().UseInfoCarrier(client);
        InfoCarrierOptionsExtension deployment = contextOptions.Options.FindExtension<InfoCarrierOptionsExtension>()!;
        Assert.DoesNotContain(typeof(DateOnly), deployment.AllowedTypes);
        Assert.True(deployment.ArbitrarySqlExecutionAllowed);
    }

    [Fact]
    public void Property_binding_and_fluent_methods_configure_the_same_options()
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Endpoint"] = "https://bound.test/data",
            ["MaxResponseBytes"] = "2048",
            ["LogForwarding:MinimumLevel"] = "Error",
            ["LogForwarding:IncludeSensitiveData"] = "false",
        }).Build();
        var client = new InfoCarrierClientOptions().WithEndpoint("https://initial.test/data");
        var server = new InfoCarrierServerOptions().WithLogForwarding(LogLevel.Warning, includeSensitiveData: true);

        config.Bind(client);
        config.Bind(server);

        Assert.Equal(new Uri("https://bound.test/data"), client.Endpoint);
        Assert.Equal(2048, client.MaxResponseBytes);
        Assert.Equal(LogLevel.Error, server.LogForwarding.MinimumLevel);
        Assert.False(server.LogForwarding.IncludeSensitiveData);
        Assert.Same(client, client.WithMaxResponseBytes(null));
        Assert.Null(client.MaxResponseBytes);
    }

    [Fact]
    public void Shared_extensions_reject_a_null_receiver()
    {
        InfoCarrierClientOptions options = null!;
        Assert.Throws<ArgumentNullException>(() => options.AddAllowedTypes(typeof(DateOnly)));
        Assert.Throws<ArgumentNullException>(() => options.AddAnonymousShapes(new { Id = 0 }));
        Assert.Throws<ArgumentNullException>(() => options.AddValueMappers(new StubMapper()));
        Assert.Throws<ArgumentNullException>(() => options.AllowArbitrarySqlExecution(true));
        Assert.Throws<ArgumentNullException>(() => options.WithEndpoint("https://example.test/data"));
    }

    private sealed class TestContext : DbContext;

    private sealed class StubMapper : IInfoCarrierValueMapper
    {
        public bool TryMapToWire(object value, Type declaredType, out object? wireValue)
        { wireValue = null; return false; }

        public bool TryMapFromWire(object? wireValue, Type declaredType, out object? value)
        { value = null; return false; }
    }
}
