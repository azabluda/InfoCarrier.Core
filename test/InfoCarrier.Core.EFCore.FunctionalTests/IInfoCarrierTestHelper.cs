namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Microsoft.Extensions.DependencyInjection;

    public interface IInfoCarrierTestHelper<TTestStore>
        where TTestStore : TestStore
    {
        IServiceCollection ConfigureInfoCarrierServices(IServiceCollection services);

        DbContextOptions BuildInfoCarrierOptions(
            TTestStore testStore,
            IServiceCollection additionalServices = null);
    }
}