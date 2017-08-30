namespace InfoCarrier.Core.Client.Infrastructure.Internal
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;

    public class InfoCarrierOptionsExtension : IDbContextOptionsExtension
    {
        public InfoCarrierOptionsExtension()
        {
        }

        public InfoCarrierOptionsExtension(InfoCarrierOptionsExtension copyFrom)
        {
            this.InfoCarrierBackend = copyFrom.InfoCarrierBackend;
        }

        public IInfoCarrierBackend InfoCarrierBackend { get; set; }

        public string LogFragment => this.InfoCarrierBackend.LogFragment;

        public virtual bool ApplyServices(IServiceCollection services)
        {
            services.AddEntityFrameworkInfoCarrierBackend();
            return true;
        }

        public virtual long GetServiceProviderHashCode() => 0;

        public virtual void Validate(IDbContextOptions options)
        {
        }
    }
}
