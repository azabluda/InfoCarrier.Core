namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    public interface IInfoCarrierTestHelper
    {
        IServiceCollection ConfigureInfoCarrierServices(IServiceCollection services);

        DbContextOptions BuildInfoCarrierOptions(IServiceCollection additionalServices = null);
    }
}