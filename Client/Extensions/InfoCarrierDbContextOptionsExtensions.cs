namespace InfoCarrier.Core.Client
{
    using System;
    using Infrastructure;
    using Infrastructure.Internal;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Query.ExpressionVisitors.Internal;

    public static class InfoCarrierDbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder<TContext> UseInfoCarrierBackend<TContext>(
            this DbContextOptionsBuilder<TContext> optionsBuilder,
            ServerContext serverContext,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
            where TContext : DbContext
            => (DbContextOptionsBuilder<TContext>)UseInfoCarrierBackend(
                (DbContextOptionsBuilder)optionsBuilder, serverContext, infoCarrierOptionsAction);

        public static DbContextOptionsBuilder UseInfoCarrierBackend(
            this DbContextOptionsBuilder optionsBuilder,
            ServerContext serverContext,
            Action<InfoCarrierDbContextOptionsBuilder> infoCarrierOptionsAction = null)
        {
            var extension = optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>();

            extension = extension != null
                ? new InfoCarrierOptionsExtension(extension)
                : new InfoCarrierOptionsExtension();

            extension.ServerContext = serverContext;

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

            infoCarrierOptionsAction?.Invoke(new InfoCarrierDbContextOptionsBuilder(optionsBuilder));

            return optionsBuilder
                .ReplaceService<IMemberAccessBindingExpressionVisitorFactory, InfoCarrierMemberAccessBindingExpressionVisitorFactory>();
        }
    }
}
