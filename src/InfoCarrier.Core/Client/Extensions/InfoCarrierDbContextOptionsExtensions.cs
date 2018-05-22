// Copyright (c) on/off it-solutions gmbh. All rights reserved.
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
        ///     Configures the context to connect to a remote service via <see cref="IInfoCarrierBackend" /> interface.
        /// </summary>
        /// <typeparam name="TContext"> The type of context being configured. </typeparam>
        /// <param name="optionsBuilder"> The builder being used to configure the context. </param>
        /// <param name="infoCarrierBackend">
        ///     The actual implementation of the <see cref="IInfoCarrierBackend" /> interface.
        /// </param>
        /// <returns> The options builder so that further configuration can be chained. </returns>
        public static DbContextOptionsBuilder<TContext> UseInfoCarrierBackend<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            IInfoCarrierBackend infoCarrierBackend)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierBackend(
                (DbContextOptionsBuilder)optionsBuilder, infoCarrierBackend);

        /// <summary>
        ///     Configures the context to connect to a remote service via <see cref="IInfoCarrierBackend" /> interface.
        /// </summary>
        /// <param name="optionsBuilder"> The builder being used to configure the context. </param>
        /// <param name="infoCarrierBackend">
        ///     The actual implementation of the <see cref="IInfoCarrierBackend" /> interface.
        /// </param>
        /// <returns> The options builder so that further configuration can be chained. </returns>
        public static DbContextOptionsBuilder UseInfoCarrierBackend(
            this DbContextOptionsBuilder optionsBuilder,
            IInfoCarrierBackend infoCarrierBackend)
        {
            if (optionsBuilder.Options.FindExtension<CoreOptionsExtension>() == null)
            {
                ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new CoreOptionsExtension());
            }

            var extension = optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>();

            extension = extension != null
                ? new InfoCarrierOptionsExtension(extension)
                : new InfoCarrierOptionsExtension();

            extension.InfoCarrierBackend = infoCarrierBackend;

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            return optionsBuilder;
        }
    }
}
