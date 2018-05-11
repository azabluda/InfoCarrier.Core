// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System;
    using System.Linq;
    using InfoCarrier.Core.FunctionalTests.TestUtilities;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.TestUtilities;
    using Xunit;

    public class ConvertToProviderTypesInfoCarrierTest : ConvertToProviderTypesTestBase<ConvertToProviderTypesInfoCarrierTest.TestFixture>
    {
        public ConvertToProviderTypesInfoCarrierTest(TestFixture fixture)
            : base(fixture)
        {
        }

        public override void Can_perform_query_with_max_length()
        {
            // UGLY: this is a complete copy-n-paste of
            // https://github.com/aspnet/EntityFrameworkCore/blob/2.1.0-preview2-final/src/EFCore.Specification.Tests/BuiltInDataTypesTestBase.cs#L27
            // We only use SequenceEqual instead of operator== for comparison of arrays.
            var shortString = "Sky";
            var shortBinary = new byte[] { 8, 8, 7, 8, 7 };
            var longString = new string('X', this.Fixture.LongStringLength);
            var longBinary = new byte[this.Fixture.LongStringLength];
            for (var i = 0; i < longBinary.Length; i++)
            {
                longBinary[i] = (byte)i;
            }

            using (var context = this.CreateContext())
            {
                context.Set<MaxLengthDataTypes>().Add(
                    new MaxLengthDataTypes
                    {
                        Id = 799,
                        String3 = shortString,
                        ByteArray5 = shortBinary,
                        String9000 = longString,
                        ByteArray9000 = longBinary,
                    });

                Assert.Equal(1, context.SaveChanges());
            }

            using (var context = this.CreateContext())
            {
                Assert.NotNull(context.Set<MaxLengthDataTypes>().Where(e => e.Id == 799 && e.String3 == shortString).ToList().SingleOrDefault());
                Assert.NotNull(context.Set<MaxLengthDataTypes>().Where(e => e.Id == 799 && e.ByteArray5.SequenceEqual(shortBinary)).ToList().SingleOrDefault());

                Assert.NotNull(context.Set<MaxLengthDataTypes>().Where(e => e.Id == 799 && e.String9000 == longString).ToList().SingleOrDefault());
                Assert.NotNull(context.Set<MaxLengthDataTypes>().Where(e => e.Id == 799 && e.ByteArray9000.SequenceEqual(longBinary)).ToList().SingleOrDefault());
            }
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

            public override DateTime DefaultDateTime => default;
        }
    }
}
