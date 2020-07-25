// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.TestModels.Inheritance;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class InheritanceInfoCarrierFixture : InheritanceFixtureBase
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

            modelBuilder.Entity<AnimalQuery>()
                .HasNoKey()
                .ToQuery(
                    () => context.Set<Bird>()
                        .Select(b => MaterializeView(b)));
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
