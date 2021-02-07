// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
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

        public override void Optional_datetime_reading_null_from_database()
        {
        }

        // Disabled: In-memory database is case-sensitive
        public override void Can_insert_and_read_back_with_case_insensitive_string_key()
        {
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Value_conversion_with_property_named_value()
        {
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Collection_property_as_scalar_Any()
        {
            base.Collection_property_as_scalar_Any();
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Collection_property_as_scalar_Count_member()
        {
            base.Collection_property_as_scalar_Count_member();
        }

        [ConditionalFact(Skip = "Issue#17050")]
        public override void Collection_enum_as_string_Contains()
        {
            base.Collection_enum_as_string_Contains();
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
