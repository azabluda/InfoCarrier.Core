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
            string databaseName,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierBackend(
                (DbContextOptionsBuilder)optionsBuilder, databaseName, infoCarrierOptionsAction);

        public static DbContextOptionsBuilder UseInfoCarrierBackend(
            this DbContextOptionsBuilder optionsBuilder,
            string databaseName,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
        {
            var extension = optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>();

            extension = extension != null
                ? new InfoCarrierOptionsExtension(extension)
                : new InfoCarrierOptionsExtension();

            if (databaseName != null)
            {
                extension.StoreName = databaseName;
            }

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            infoCarrierOptionsAction?.Invoke(new InfoCarrierDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }

        public static DbContextOptionsBuilder<TContext> UseInfoCarrierBackend<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierBackend(
                (DbContextOptionsBuilder)optionsBuilder, infoCarrierOptionsAction);

        public static DbContextOptionsBuilder UseInfoCarrierBackend(
            this DbContextOptionsBuilder optionsBuilder,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
        {
            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new InfoCarrierOptionsExtension());

            infoCarrierOptionsAction?.Invoke(new InfoCarrierDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder;
        }
    }
}
