﻿namespace InfoCarrier.Core.EFCore.FunctionalTests.InMemory
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class DataAnnotationInfoCarrierTest : DataAnnotationTestBase<TestStoreBase, DataAnnotationInfoCarrierFixture>
    {
        public DataAnnotationInfoCarrierTest(DataAnnotationInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        public override ModelBuilder DatabaseGeneratedOption_configures_the_property_correctly()
        {
            var modelBuilder = base.DatabaseGeneratedOption_configures_the_property_correctly();

            var identity = modelBuilder.Model.FindEntityType(typeof(GeneratedEntity)).FindProperty(nameof(GeneratedEntity.Identity));
            Assert.False(identity.RequiresValueGenerator);

            return modelBuilder;
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
                Assert.True(context.Model.FindEntityType(typeof(BookDetail)).FindNavigation("Book").ForeignKey.IsRequired);
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
    }
}