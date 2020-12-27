// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using InfoCarrier.Core.Common.ValueMapping;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.InMemory.Internal;
    using Microsoft.EntityFrameworkCore.InMemory.Storage.Internal;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;

    public class InMemoryTestStore : InfoCarrierBackendTestStore
    {
        public InMemoryTestStore(string name, bool shared, SharedTestStoreProperties testStoreProperties)
            : base(name, shared, testStoreProperties)
        {
        }

        protected override IServiceCollection AddServices(IServiceCollection serviceCollection)
            => serviceCollection.AddEntityFrameworkInMemoryDatabase()
                .AddSingleton<IInfoCarrierValueMapper, InfoCarrierNetTopologySuiteValueMapper>()
                .AddSingleton<TestStoreIndex>();

        protected override TestStoreIndex GetTestStoreIndex(IServiceProvider serviceProvider)
            => serviceProvider == null
                ? base.GetTestStoreIndex(null)
                : serviceProvider.GetRequiredService<TestStoreIndex>();

        public override DbContextOptionsBuilder AddProviderOptions(DbContextOptionsBuilder builder)
            => base.AddProviderOptions(builder).UseInMemoryDatabase(this.Name);

        private void TransactionIgnoredWarning()
        {
            this.ServiceProvider.GetRequiredService<IDiagnosticsLogger<DbLoggerCategory.Database.Transaction>>().TransactionIgnoredWarning();
        }

        public override void BeginTransaction()
        {
            this.TransactionIgnoredWarning();
        }

        public override Task BeginTransactionAsync(CancellationToken cancellationToken)
        {
            this.TransactionIgnoredWarning();
            return Task.CompletedTask;
        }

        public override void CommitTransaction()
        {
            this.TransactionIgnoredWarning();
        }

        public override void RollbackTransaction()
        {
            this.TransactionIgnoredWarning();
        }

        public override void Clean(DbContext context)
        {
            context.GetService<IInMemoryStoreCache>().GetStore(this.Name).Clear();
            context.Database.EnsureCreated();
        }
    }
}
