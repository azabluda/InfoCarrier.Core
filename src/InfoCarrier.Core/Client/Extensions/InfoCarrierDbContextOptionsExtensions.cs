// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

// ReSharper disable once CheckNamespace
namespace InfoCarrier.Core.Client
{
    using InfoCarrier.Core.Client.Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;

    /// <summary>
    ///     InfoCarrier specific extension methods for <see cref="DbContextOptionsBuilder" />.
    /// </summary>
    public static class InfoCarrierDbContextOptionsExtensions
    {
        /// <summary>
        ///     Configures the context to connect to a remote service via <see cref="IInfoCarrierClient" /> interface.
        /// </summary>
        /// <typeparam name="TContext"> The type of context being configured. </typeparam>
        /// <param name="optionsBuilder"> The builder being used to configure the context. </param>
        /// <param name="infoCarrierClient">
        ///     The actual implementation of the <see cref="IInfoCarrierClient" /> interface.
        /// </param>
        /// <returns> The options builder so that further configuration can be chained. </returns>
        public static DbContextOptionsBuilder<TContext> UseInfoCarrierClient<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            IInfoCarrierClient infoCarrierClient)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierClient(
                (DbContextOptionsBuilder)optionsBuilder, infoCarrierClient);

        /// <summary>
        ///     Configures the context to connect to a remote service via <see cref="IInfoCarrierClient" /> interface.
        /// </summary>
        /// <param name="optionsBuilder"> The builder being used to configure the context. </param>
        /// <param name="infoCarrierClient">
        ///     The actual implementation of the <see cref="IInfoCarrierClient" /> interface.
        /// </param>
        /// <returns> The options builder so that further configuration can be chained. </returns>
        public static DbContextOptionsBuilder UseInfoCarrierClient(
            this DbContextOptionsBuilder optionsBuilder,
            IInfoCarrierClient infoCarrierClient)
        {
            if (optionsBuilder.Options.FindExtension<CoreOptionsExtension>() == null)
            {
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new CoreOptionsExtension());
            }

            var extension = optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>();

            extension = extension != null
                ? new InfoCarrierOptionsExtension(extension)
                : new InfoCarrierOptionsExtension();

            extension.InfoCarrierClient = infoCarrierClient;

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }
    }
}
