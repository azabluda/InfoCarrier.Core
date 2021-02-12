// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Collections.Generic;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public abstract class SaveChangesInterceptionInfoCarrierTestBase : SaveChangesInterceptionTestBase
    {
        protected SaveChangesInterceptionInfoCarrierTestBase(InterceptionInfoCarrierFixtureBase fixture)
            : base(fixture)
        {
            fixture.Reseed();
        }

        protected override bool SupportsOptimisticConcurrency
            => false;

        public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override string StoreName
                => "SaveChangesInterception";

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override IServiceCollection InjectInterceptors(
                IServiceCollection serviceCollection,
                IEnumerable<IInterceptor> injectedInterceptors)
                => base.InjectInterceptors(this.TestStoreFactory.AddProviderServices(serviceCollection), injectedInterceptors);
        }

        public class SaveChangesInterceptionInfoCarrierTest
            : SaveChangesInterceptionInfoCarrierTestBase, IClassFixture<SaveChangesInterceptionInfoCarrierTest.TestFixture>
        {
            public SaveChangesInterceptionInfoCarrierTest(TestFixture fixture)
                : base(fixture)
            {
            }

            public class TestFixture : InterceptionInfoCarrierFixtureBase
            {
                protected override bool ShouldSubscribeToDiagnosticListener
                    => false;
            }
        }

        public class SaveChangesInterceptionWithDiagnosticsInfoCarrierTest
            : SaveChangesInterceptionInfoCarrierTestBase,
                IClassFixture<SaveChangesInterceptionWithDiagnosticsInfoCarrierTest.TestFixture>
        {
            public SaveChangesInterceptionWithDiagnosticsInfoCarrierTest(TestFixture fixture)
                : base(fixture)
            {
            }

            public class TestFixture : InterceptionInfoCarrierFixtureBase
            {
                protected override bool ShouldSubscribeToDiagnosticListener
                    => true;
            }
        }
    }
}
