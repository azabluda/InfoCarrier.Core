namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Storage;

    public class InfoCarrierDatabaseCreator : IDatabaseCreator
    {
        private readonly IModel model;
        private readonly IDatabase database;

        public InfoCarrierDatabaseCreator(IDatabase database, IModel model)
        {
            this.database = database;
            this.model = model;
        }

        public virtual bool EnsureDeleted() => false;

        public virtual Task<bool> EnsureDeletedAsync(CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(this.EnsureDeleted());

        public virtual bool EnsureCreated() => false;

        public virtual Task<bool> EnsureCreatedAsync(CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(this.EnsureCreated());
    }
}
