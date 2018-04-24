// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using TestUtilities;
    using Xunit;

    public class DataAnnotationInfoCarrierTest : DataAnnotationTestBase<DataAnnotationInfoCarrierTest.TestFixture>
    {
        public DataAnnotationInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public override void ConcurrencyCheckAttribute_throws_if_value_in_database_changed()
        {
            using (var context = this.CreateContext())
            {
                Assert.True(context.Model.FindEntityType(typeof(One)).FindProperty("RowVersion").IsConcurrencyToken);
            }
        }

        public override void MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        {
            using (var context = this.CreateContext())
            {
                Assert.Equal(10, context.Model.FindEntityType(typeof(One)).FindProperty("MaxLengthProperty").GetMaxLength());
            }
        }

        public override void RequiredAttribute_for_navigation_throws_while_inserting_null_value()
        {
            using (var context = this.CreateContext())
            {
                Assert.True(context.Model.FindEntityType(typeof(BookDetails)).FindNavigation(nameof(BookDetails.AnotherBook)).ForeignKey.IsRequired);
            }
        }

        public override void RequiredAttribute_for_property_throws_while_inserting_null_value()
        {
            using (var context = this.CreateContext())
            {
                Assert.False(context.Model.FindEntityType(typeof(One)).FindProperty("RequiredColumn").IsNullable);
            }
        }

        public override void StringLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        {
            using (var context = this.CreateContext())
            {
                Assert.Equal(16, context.Model.FindEntityType(typeof(Two)).FindProperty("Data").GetMaxLength());
            }
        }

        public override void TimestampAttribute_throws_if_value_in_database_changed()
        {
            using (var context = this.CreateContext())
            {
                Assert.True(context.Model.FindEntityType(typeof(Two)).FindProperty("Timestamp").IsConcurrencyToken);
            }
        }

        public class TestFixture : DataAnnotationFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.CreateOrGet(
                    ref this.testStoreFactory,
                    this.ContextType,
                    this.OnModelCreating,
                    InfoCarrierTestStoreFactory.InMemory,
                    o => o.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        }
    }
}
