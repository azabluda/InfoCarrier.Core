// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

public partial class ServerParameterizationTest
{
    /// <summary>
    ///     An unregistered public setter helper is refused on the client, before EF renames the
    ///     selector parameter. Registration must not be inferred from the helper being public.
    /// </summary>
    [ConditionalFact]
    public async Task An_unregistered_public_setter_helper_keeps_the_client_diagnostic()
    {
        Run run = await RunBothWays(null, InvalidSetter);

        var wire = Assert.IsType<InvalidOperationException>(run.WireError);
        var direct = Assert.IsType<InvalidOperationException>(run.DirectError);
        Assert.Empty(run.OverTheWire);
        Assert.False(wire.Data.Contains(InfoCarrierFaultMapper.ServerStackTraceKey));
        Assert.NotEqual(direct.Message, wire.Message);
        Assert.Contains(
            RelationalStrings.InvalidPropertyInSetProperty("e => e    .MaybeScalar(x => x.Id)"),
            wire.Message.Replace("\r", string.Empty).Replace("\n", string.Empty),
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Registering the same public helper on both halves lets EF validate the setter and
    ///     produce the same selector diagnostic as a direct query, without executing an update.
    ///     The full query diagnostic still uses a different name for the value parameter.
    /// </summary>
    [ConditionalFact]
    public async Task A_registered_public_setter_helper_uses_the_backend_diagnostic()
    {
        Run run = await RunBothWays([typeof(TestExtensions)], InvalidSetter);

        var wire = Assert.IsType<InvalidOperationException>(run.WireError);
        var direct = Assert.IsType<InvalidOperationException>(run.DirectError);
        Assert.Empty(run.OverTheWire);
        string details = RelationalStrings.InvalidPropertyInSetProperty("b => b    .MaybeScalar(x => x.Id)");
        Assert.Contains(details, direct.Message.Replace("\r", string.Empty).Replace("\n", string.Empty), StringComparison.Ordinal);
        Assert.Contains(details, wire.Message.Replace("\r", string.Empty).Replace("\n", string.Empty), StringComparison.Ordinal);
        Assert.Contains("@p", direct.Message, StringComparison.Ordinal);
        Assert.Contains("@Value", wire.Message, StringComparison.Ordinal);
        Assert.IsType<string>(wire.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
    }

    private static async Task InvalidSetter(DbContext context)
        => _ = await context.Set<TestUtilities.Blog>()
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.MaybeScalar(x => x.Id), 1));

    /// <summary>
    ///     Without an explicit cast-target registration, the client refuses at the cast and has
    ///     no backend stack. Adding a public interface to a query must not implicitly admit it.
    /// </summary>
    [ConditionalFact]
    public async Task An_unregistered_cast_target_is_refused_before_the_filter_reaches_the_backend()
    {
        Run run = await RunBothWays(null, CastAndFilter);

        var wire = Assert.IsType<InvalidOperationException>(run.WireError);
        Assert.IsType<InvalidOperationException>(run.DirectError);
        Assert.Empty(run.OverTheWire);
        Assert.False(wire.Data.Contains(InfoCarrierFaultMapper.ServerStackTraceKey));
        Assert.DoesNotContain(".Where(", wire.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Registering the cast target sends the filter to EF. Both providers refuse, but the
    ///     diagnostic parameter name remains a wire difference rather than an earlier refusal.
    /// </summary>
    [ConditionalFact]
    public async Task A_registered_cast_target_reaches_the_backend_with_its_filter()
    {
        Run run = await RunBothWays([typeof(IConfigurationEntity)], CastAndFilter);

        var wire = Assert.IsType<InvalidOperationException>(run.WireError);
        var direct = Assert.IsType<InvalidOperationException>(run.DirectError);
        Assert.Empty(run.OverTheWire);
        Assert.IsType<string>(wire.Data[InfoCarrierFaultMapper.ServerStackTraceKey]);
        Assert.Contains(".Where(", wire.Message, StringComparison.Ordinal);
        Assert.Contains("@Value", wire.Message, StringComparison.Ordinal);
        Assert.Contains("@id", direct.Message, StringComparison.Ordinal);
    }

    private static Task CastAndFilter(DbContext context)
    {
        var id = 1;
        _ = context.Set<TestUtilities.Blog>().Cast<IConfigurationEntity>().FirstOrDefault(e => e.Id == id);
        return Task.CompletedTask;
    }

    public interface IConfigurationEntity
    {
        int Id { get; }
    }
}
