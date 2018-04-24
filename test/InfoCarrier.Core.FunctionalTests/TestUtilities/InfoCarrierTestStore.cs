// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using Client;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class InfoCarrierTestStore : TestStore
    {
        private readonly TestInfoCarrierBackend backend;

        public InfoCarrierTestStore(TestInfoCarrierBackend backend)
            : base(null, false)
        {
            this.backend = backend;
        }

        public override void Dispose()
        {
            this.backend.Dispose();
            base.Dispose();
        }

        protected override void Initialize(Func<DbContext> createContext, Action<DbContext> seed)
        {
            this.backend.Initialize(seed);
        }

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => builder.UseInfoCarrierBackend(this.backend);

        public override void Clean(DbContext context)
        {
            this.backend.Clean();
        }
    }
}
