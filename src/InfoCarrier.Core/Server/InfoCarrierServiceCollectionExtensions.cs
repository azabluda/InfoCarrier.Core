// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Server
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    ///     InfoCarrier server specific extension methods for <see cref="IServiceCollection" />.
    /// </summary>
    public static class InfoCarrierServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the server related services required by InfoCarrier.Core.
        /// </summary>
        /// <param name="serviceCollection"> The <see cref="IServiceCollection" /> to add services to. </param>
        /// <returns>
        /// The same service collection so that multiple calls can be chained.
        /// </returns>
        public static IServiceCollection AddInfoCarrierServer(this IServiceCollection serviceCollection)
        {
            new ServiceCollectionMap(serviceCollection)
                .TryAddScoped<IInfoCarrierServer, InfoCarrierServer>();

            return serviceCollection;
        }
    }
}
