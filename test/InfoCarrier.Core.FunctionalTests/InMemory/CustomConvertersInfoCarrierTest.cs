// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Threading.Tasks;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class CustomConvertersInfoCarrierTest : CustomConvertersTestBase<CustomConvertersInfoCarrierTest.TestFixture>
    {
        public CustomConvertersInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        [ConditionalFact(Skip = "TODO: Better translation of sequential equality")]
        public override void Can_perform_query_with_max_length()
        {
            base.Can_perform_query_with_max_length();
        }

        [ConditionalFact(Skip = "Disabled: In-memory database is case-sensitive")]
        public override void Can_insert_and_read_back_with_case_insensitive_string_key()
        {
            base.Can_insert_and_read_back_with_case_insensitive_string_key();
        }

        [ConditionalTheory(Skip = "Issue#14042")]
        [InlineData(true)]
        [InlineData(false)]
        public override Task Can_query_custom_type_not_mapped_by_default_equality(bool isAsync)
        {
            return base.Can_query_custom_type_not_mapped_by_default_equality(isAsync);
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Value_conversion_with_property_named_value()
        {
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Collection_property_as_scalar()
        {
            base.Collection_property_as_scalar();
        }

        public class TestFixture : CustomConvertersFixtureBase
        {
            private ITestStoreFactory testStoreFactory;

            protected override ITestStoreFactory TestStoreFactory =>
                InfoCarrierTestStoreFactory.EnsureInitialized(
                    ref this.testStoreFactory,
                    InfoCarrierTestStoreFactory.InMemory,
                    this.ContextType,
                    this.OnModelCreating);

            public override bool StrictEquality => true;

            public override bool SupportsAnsi => false;

            public override bool SupportsUnicodeToAnsiConversion => true;

            public override bool SupportsLargeStringComparisons => true;

            public override bool SupportsBinaryKeys => false;

            public override bool SupportsDecimalComparisons => true;

            public override DateTime DefaultDateTime => default;
        }
    }
}
