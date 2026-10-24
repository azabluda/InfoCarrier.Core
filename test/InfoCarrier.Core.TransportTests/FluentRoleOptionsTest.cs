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
    public void Shared_operations_and_generic_helpers_preserve_client_type_in_any_order()
    {
        var original = new InfoCarrierClientOptions();
        InfoCarrierClientOptions result = original
            .AddAnonymousShapes(new { Label = "" })
            .WithEndpoint("https://example.test/data")
            .AddValueMappers(new StubMapper())
            .WithMaxResponseBytes(4096)
            .ConfigureFluentTestDeployment()
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
    public void Shared_operations_and_generic_helpers_preserve_server_type_in_any_order()
    {
        var original = new InfoCarrierServerOptions();
        InfoCarrierServerOptions result = original
            .AddAllowedTypes(typeof(DateOnly))
            .WithMaxRequestBytes(8192)
            .AddValueMappers(new StubMapper())
            .WithStoresOwnedDataInsideOwner(true)
            .ConfigureFluentTestDeployment()
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
            .WithServerUsesRelationalQueryRules(false));

        mutable!.WithEndpoint("https://second.test/data")
            .WithMaxResponseBytes(null)
            .WithServerUsesRelationalQueryRules(true);

        InfoCarrierClientRegistration registration = ((IInfoCarrierClientConfiguration)client).Registration!;
        Assert.Equal(new Uri("https://first.test/data"), registration.Endpoint);
        Assert.Equal(1024, registration.MaxResponseBytes);
        Assert.False(registration.ServerUsesRelationalQueryRules);
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

internal static class FluentTestDeployment
{
    internal static TBuilder ConfigureFluentTestDeployment<TBuilder>(this TBuilder builder)
        where TBuilder : InfoCarrierDeploymentBuilder
        => builder.AddAllowedTypes(typeof(TimeOnly))
            .AllowArbitrarySqlExecution(true)
            .AddAnonymousShapes(new { Id = 0 });
}
