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
            this.StoreName = copyFrom.StoreName;
        }

        public string StoreName { get; set; }

        public virtual void ApplyServices(IServiceCollection services)
        {
            services.AddEntityFrameworkInfoCarrierBackend();
        }
    }
}
