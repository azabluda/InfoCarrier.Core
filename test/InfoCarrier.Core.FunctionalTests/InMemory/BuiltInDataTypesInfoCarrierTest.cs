﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;

    public class BuiltInDataTypesInfoCarrierTest : BuiltInDataTypesTestBase<BuiltInDataTypesInfoCarrierTest.TestFixture>
    {
        public BuiltInDataTypesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public class TestFixture : BuiltInDataTypesFixtureBase
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
