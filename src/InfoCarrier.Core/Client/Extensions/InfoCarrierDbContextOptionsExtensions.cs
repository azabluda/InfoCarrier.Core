namespace InfoCarrier.Core.Client
{
    using System;
    using Infrastructure;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;

    public static class InfoCarrierDbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder<TContext> UseInfoCarrierBackend<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            IInfoCarrierBackend infoCarrierBackend,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierBackend(
                (DbContextOptionsBuilder)optionsBuilder, infoCarrierBackend, infoCarrierOptionsAction);

        public static DbContextOptionsBuilder UseInfoCarrierBackend(
            this DbContextOptionsBuilder optionsBuilder,
            IInfoCarrierBackend infoCarrierBackend,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
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

            infoCarrierOptionsAction?.Invoke(new InfoCarrierDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }
    }
}
