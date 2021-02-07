// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class ConvertToProviderTypesInfoCarrierTest : ConvertToProviderTypesTestBase<ConvertToProviderTypesInfoCarrierTest.TestFixture>
    {
        public ConvertToProviderTypesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public override void Optional_datetime_reading_null_from_database()
        {
        }

        public class TestFixture : ConvertToProviderTypesFixtureBase
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
