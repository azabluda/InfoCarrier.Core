namespace InfoCarrier.Core.Client.Infrastructure
{
    using Microsoft.EntityFrameworkCore;

    public class InfoCarrierDbContextOptionsBuilder
    {
        public InfoCarrierDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
        {
            this.OptionsBuilder = optionsBuilder;
        }

        protected virtual DbContextOptionsBuilder OptionsBuilder { get; }
    }
}
