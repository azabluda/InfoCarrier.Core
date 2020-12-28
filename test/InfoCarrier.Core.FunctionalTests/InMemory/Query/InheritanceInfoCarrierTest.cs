// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query
{
    using System;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Query;
    using Xunit;
    using Xunit.Abstractions;

    public class InheritanceInfoCarrierTest : InheritanceTestBase<InheritanceInfoCarrierFixture>
    {
        public InheritanceInfoCarrierTest(InheritanceInfoCarrierFixture fixture, ITestOutputHelper testOutputHelper)
            : base(fixture)
        {
        }

        protected override bool EnforcesFkConstraints => false;

        [ConditionalFact]
        public override void Can_query_all_animal_views()
        {
            var message = Assert.Throws<InvalidOperationException>(() => base.Can_query_all_animal_views()).Message;

            Assert.Equal(
                CoreStrings.TranslationFailed(
                    @"DbSet<Bird>
    .Select(b => InheritanceInfoCarrierFixture.MaterializeView(b))
    .OrderBy(a => a.CountryId)"),
                message,
                ignoreLineEndingDifferences: true);
        }
    }
}
