// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core.Wire;

/// <summary>Registers protocol selection and its built-in singleton codecs.</summary>
public static class InfoCarrierWireProtocolServiceCollectionExtensions
{
    /// <summary>Registers default codecs once. Hosts can replace the codec collection after role registration.</summary>
    public static IServiceCollection AddInfoCarrierWireProtocols(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWireProtocolCodec, CurrentWireProtocolCodec>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWireProtocolCodec, Protocol3WireProtocolCodec>());
        services.TryAddSingleton<InfoCarrierWireProtocol>();
        return services;
    }
}
