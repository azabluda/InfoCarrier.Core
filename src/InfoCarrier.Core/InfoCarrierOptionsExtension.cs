// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     The EF Core options extension that carries the client's <see cref="IInfoCarrierClient" />
///     and registers InfoCarrier provider services (DI-first, requirements §4.2). Added by
///     <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, IInfoCarrierClient, System.Action{InfoCarrierDbContextOptionsBuilder})" />.
/// </summary>
public class InfoCarrierOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>
    ///     The client used to ship operations to the server. Resolved from options (v1 pattern)
    ///     so the provider can be constructed per-context.
    /// </summary>
    public virtual IInfoCarrierClient? InfoCarrierClient { get; private set; }

    /// <summary>
    ///     Configures the client for this options instance.
    /// </summary>
    public virtual InfoCarrierOptionsExtension WithInfoCarrierClient(IInfoCarrierClient client)
    {
        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.InfoCarrierClient = client;
        return clone;
    }

    /// <summary>
    ///     The CLR types this client may name in a query beyond the ones its model implies
    ///     (ADR-008 constraint 2). Empty unless the application registered some — see
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" />.
    /// </summary>
    public virtual IReadOnlyList<Type> AllowedTypes { get; private set; } = [];

    /// <summary>
    ///     Adds to <see cref="AllowedTypes" /> for this options instance.
    /// </summary>
    /// <remarks>
    ///     Additive rather than replacing, so two calls to <c>AllowTypes</c> both count. Every
    ///     other <c>With…</c> on an EF options extension replaces, but this one names a set and a
    ///     caller configuring options in two places would silently lose the first list.
    /// </remarks>
    public virtual InfoCarrierOptionsExtension WithAllowedTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.AllowedTypes = [.. AllowedTypes, .. types];
        return clone;
    }

    /// <summary>
    ///     Whether this client may send a query carrying raw SQL (#60). <c>false</c> unless the
    ///     application called
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />.
    /// </summary>
    public virtual bool ArbitrarySqlExecutionAllowed { get; private set; }

    /// <summary>
    ///     Sets <see cref="ArbitrarySqlExecutionAllowed" /> for this options instance.
    /// </summary>
    public virtual InfoCarrierOptionsExtension WithArbitrarySqlExecution()
    {
        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.ArbitrarySqlExecutionAllowed = true;
        return clone;
    }


    /// <summary>
    ///     Whether the context's own options permit sending raw SQL, read <em>per execution</em>
    ///     for the reason <see cref="AllowedTypesFor" /> records.
    /// </summary>
    internal static bool ArbitrarySqlExecutionAllowedFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.ArbitrarySqlExecutionAllowed ?? false;

    /// <summary>
    ///     The types the context's own options admit, read <em>per execution</em>.
    /// </summary>
    /// <remarks>
    ///     Per execution and not captured, for the reason <c>InfoCarrierDatabase.ClientFor</c>
    ///     records: what <c>CompileQuery</c> returns is cached across every context sharing an
    ///     options shape, so anything per-context has to be resolved when the query runs.
    /// </remarks>
    internal static IReadOnlyList<Type> AllowedTypesFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.AllowedTypes ?? [];

    /// <inheritdoc />
    public virtual DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    public virtual void Validate(IDbContextOptions options)
    {
        if (InfoCarrierClient is null)
        {
            throw new InvalidOperationException(
                "InfoCarrier requires an IInfoCarrierClient. Call UseInfoCarrier(client).");
        }
    }

    private sealed class ExtensionInfo(InfoCarrierOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "using InfoCarrier ";

        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["InfoCarrier"] = "1";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
