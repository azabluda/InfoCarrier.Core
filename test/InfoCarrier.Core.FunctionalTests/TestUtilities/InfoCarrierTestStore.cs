// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using InfoCarrier.Core.Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class InfoCarrierTestStore : TestStore
    {
        private readonly InfoCarrierBackendTestStore backend;

        public InfoCarrierTestStore(InfoCarrierBackendTestStore backend)
            : base(null, false)
        {
            this.backend = backend;
        }

        public override void Dispose()
        {
            this.backend.Dispose();
            base.Dispose();
        }

        public override TestStore Initialize(IServiceProvider serviceProvider, Func<DbContext> createContext, Action<DbContext> seed = null, Action<DbContext> clean = null)
        {
            this.backend.Initialize(this.backend.ServiceProvider, this.backend.CreateDbContext, seed);
            return this;
        }

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => builder.UseInfoCarrierClient(this.backend);

        public override void Clean(DbContext context)
        {
            using (var dbContext = this.backend.CreateDbContext())
            {
                this.backend.Clean(dbContext);
            }
        }
    }
}
