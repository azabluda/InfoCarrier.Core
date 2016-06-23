namespace InfoCarrier.Core.Client.Storage.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.EntityFrameworkCore.Update;
    using Microsoft.Extensions.Logging;
    using Remotion.Linq;

    public class InfoCarrierDatabase : Database, IInfoCarrierDatabase
    {
        //private readonly IInfoCarrierStore _store;
        private readonly ILogger<InfoCarrierDatabase> logger;

        public InfoCarrierDatabase(
            IQueryCompilationContextFactory queryCompilationContextFactory,
            //IInfoCarrierStoreSource storeSource,
            //IDbContextOptions options,
            ILogger<InfoCarrierDatabase> logger)
            : base(queryCompilationContextFactory)
        {
            this.logger = logger;
            //_store = storeSource.GetStore(options);
        }

        public override int SaveChanges(IReadOnlyList<IUpdateEntry> entries)
        {
            throw new NotImplementedException();
        }

        public override Task<int> SaveChangesAsync(
            IReadOnlyList<IUpdateEntry> entries,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }
    }
}
