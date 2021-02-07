// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.InheritanceModel;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class InheritanceQueryInfoCarrierTest : InheritanceQueryTestBase<InheritanceQueryInfoCarrierTest.TestFixture>
    {
        public InheritanceQueryInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        protected override bool EnforcesFkConstraints
            => false;

        public override async Task Can_query_all_animal_views(bool async)
        {
            var message = (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Can_query_all_animal_views(async))).Message;

            Assert.Equal(
                CoreStrings.TranslationFailed(
                    @"DbSet<Bird>()
    .Select(b => TestFixture.MaterializeView(b))
    .OrderBy(a => a.CountryId)"),
                message,
                ignoreLineEndingDifferences: true);
        }

        public class TestFixture : InheritanceQueryFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating,
                    o => o.ConfigureWarnings(c => c.Log(InMemoryEventId.TransactionIgnoredWarning)));

            protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
            {
                base.OnModelCreating(modelBuilder, context);

                modelBuilder.Entity<AnimalQuery>().ToInMemoryQuery(() => context.Set<Bird>().Select(b => MaterializeView(b)));
            }

            private static AnimalQuery MaterializeView(Bird bird)
            {
                switch (bird)
                {
                    case Kiwi kiwi:
                        return new KiwiQuery
                        {
                            Name = kiwi.Name,
                            CountryId = kiwi.CountryId,
                            EagleId = kiwi.EagleId,
                            FoundOn = kiwi.FoundOn,
                            IsFlightless = kiwi.IsFlightless,
                        };
                    case Eagle eagle:
                        return new EagleQuery
                        {
                            Name = eagle.Name,
                            CountryId = eagle.CountryId,
                            EagleId = eagle.EagleId,
                            Group = eagle.Group,
                            IsFlightless = eagle.IsFlightless,
                        };
                }

                throw new InvalidOperationException();
            }
        }
    }
}
