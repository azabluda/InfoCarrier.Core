// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     The EF Core options extension that carries the client's <see cref="IInfoCarrierClient" />
///     and registers InfoCarrier provider services (DI-first, requirements §4.2). Added by
///     <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier" />.
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

    private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
    {
        public ExtensionInfo(InfoCarrierOptionsExtension extension)
            : base(extension)
        {
        }

        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "using InfoCarrier ";

        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["InfoCarrier"] = "1";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
