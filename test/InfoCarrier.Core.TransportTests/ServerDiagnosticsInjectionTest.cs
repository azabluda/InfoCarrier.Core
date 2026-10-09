// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics;
using InfoCarrier.Core.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ServerDiagnosticsInjectionTest
{
    [Fact]
    public void Hosts_have_independent_limits_and_use_the_current_scoped_logger()
    {
        var logs = new FailureLogs();
        var services = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics();
        services.AddScoped<ILoggerFactory>(_ => LoggerFactory.Create(b => b.AddProvider(logs)));
        using var first = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var second = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        foreach (var host in new[] { first, second })
        {
            var diagnostics = host.GetRequiredService<InfoCarrierServerDiagnostics>();
            for (int i = 0; i < 8; i++)
            {
                using var scope = host.CreateScope();
                using var request = diagnostics.BeginRequest(InfoCarrierOperation.Query,
                    scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
                diagnostics.ReportFailure(new InvalidOperationException("secret"));
                diagnostics.ReportFailure(new InvalidOperationException("duplicate"));
            }
        }
        Assert.Equal(10, logs.Entries.Count);
        Assert.Equal(10, logs.Entries.Select(e => e.Properties["RequestId"]).Distinct().Count());
    }

    [Fact]
    public void Registration_respects_the_injected_clock_and_needs_no_logger()
    {
        var clock = new EnvelopeFailureLoggingTest.ManualClock();
        var services = new ServiceCollection().AddMetrics();
        services.AddSingleton<TimeProvider>(clock);
        services.AddInfoCarrierServerDiagnostics();
        services.AddInfoCarrierServerDiagnostics();
        using var host = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        Assert.Same(clock, host.GetRequiredService<TimeProvider>());
        var diagnostics = Assert.Single(host.GetServices<InfoCarrierServerDiagnostics>());
        using var request = diagnostics.BeginRequest(null);
        diagnostics.ReportFailure(new InvalidOperationException("secret"));
    }



    [Theory]
    [InlineData(true, InfoCarrierServerPhase.Input, "Cancellation", LogLevel.Information)]
    [InlineData(true, InfoCarrierServerPhase.ResponseWrite, "Cancellation", LogLevel.Information)]
    [InlineData(false, InfoCarrierServerPhase.ResponseWrite, "ResponseWrite", LogLevel.Error)]
    public void Aborted_transport_io_uses_the_request_token(bool cancelled, InfoCarrierServerPhase phase, string reason, LogLevel level)
    {
        var logs = new FailureLogs();
        using var logger = LoggerFactory.Create(b => b.AddProvider(logs));
        using var provider = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics().BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<InfoCarrierServerDiagnostics>();
        using var token = new CancellationTokenSource();
        if (cancelled) { token.Cancel(); }
        using var request = diagnostics.BeginRequest(null, logger);
        diagnostics.SetPhase(phase);
        diagnostics.ReportFailure(new IOException("secret-disconnection"), token.Token);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(reason, entry.Properties["Reason"]);
        Assert.Equal(level, entry.Level);
    }

    [Fact]
    public void Refusal_floods_do_not_suppress_unexpected_failure_reasons()
    {
        var logs = new FailureLogs();
        using var logger = LoggerFactory.Create(b => b.AddProvider(logs));
        using var provider = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics().BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<InfoCarrierServerDiagnostics>();
        for (int i = 0; i < 100; i++)
        {
            using var request = diagnostics.BeginRequest(InfoCarrierOperation.Query, logger);
            diagnostics.ReportFailure(new InvalidOperationException("secret"), reason: InfoCarrierServerFailureReason.Permission);
        }
        using (diagnostics.BeginRequest(InfoCarrierOperation.Query, logger))
        {
            diagnostics.ReportFailure(new InvalidOperationException("secret"));
        }
        Assert.Equal(6, logs.Entries.Count);
        var unexpected = Assert.Single(logs.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal("ExecutionFailure", unexpected.Properties["Reason"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Caller_context_is_opt_in_pseudonymous_and_host_specific(bool includeCaller)
    {
        var logs = new FailureLogs();
        var services = new ServiceCollection().AddMetrics().AddInfoCarrierServerDiagnostics(includeCaller);
        using var logger = LoggerFactory.Create(b => b.AddProvider(logs));
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();
        using var activity = new Activity("shared-trace").Start();
        foreach (var host in new[] { first, first, second })
        {
            using var request = host.GetRequiredService<InfoCarrierServerDiagnostics>()
                .BeginRequest(InfoCarrierOperation.Query, logger, "secret-caller\n");
            host.GetRequiredService<InfoCarrierServerDiagnostics>().ReportFailure(new InvalidOperationException("secret"));
        }
        var entries = logs.Entries.ToArray();
        Assert.Equal(3, entries.Length);
        Assert.Equal(3, entries.Select(e => e.Properties["RequestId"]).Distinct().Count());
        Assert.All(entries, e => Assert.DoesNotContain("secret", e.Message));
        if (includeCaller)
        {
            Assert.Equal(64, entries[0].Properties["CallerId"]!.ToString()!.Length);
            Assert.Equal(entries[0].Properties["CallerId"], entries[1].Properties["CallerId"]);
            Assert.NotEqual(entries[0].Properties["CallerId"], entries[2].Properties["CallerId"]);
        }
        else
        {
            Assert.All(entries, e => Assert.Null(e.Properties["CallerId"]));
        }
    }
}
