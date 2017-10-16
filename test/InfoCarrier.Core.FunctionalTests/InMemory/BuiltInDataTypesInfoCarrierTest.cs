// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.InMemory
{
    using System.Linq;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class BuiltInDataTypesInfoCarrierTest : BuiltInDataTypesTestBase<BuiltInDataTypesInfoCarrierFixture>
    {
        public BuiltInDataTypesInfoCarrierTest(BuiltInDataTypesInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public virtual void Can_perform_query_with_ansi_strings()
        {
            this.Can_perform_query_with_ansi_strings_test(supportsAnsi: false);
        }

        [Fact]
        public override void Can_perform_query_with_max_length()
        {
            // UGLY: this is a complete copy-n-paste of
            // https://github.com/aspnet/EntityFramework/blob/rel/1.1.0/src/Microsoft.EntityFrameworkCore.Specification.Tests/BuiltInDataTypesTestBase.cs#L25
            // We only use SequenceEqual instead of operator== for comparison of arrays.
            var shortString = "Sky";
            var shortBinary = new byte[] { 8, 8, 7, 8, 7 };
            var longString = new string('X', 9000);
            var longBinary = new byte[9000];
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
                Assert.NotNull(context.Set<MaxLengthDataTypes>().SingleOrDefault(e => e.Id == 799 && e.String3 == shortString));
                Assert.NotNull(context.Set<MaxLengthDataTypes>().SingleOrDefault(e => e.Id == 799 && e.ByteArray5.SequenceEqual(shortBinary)));

                Assert.NotNull(context.Set<MaxLengthDataTypes>().SingleOrDefault(e => e.Id == 799 && e.String9000 == longString));
                Assert.NotNull(context.Set<MaxLengthDataTypes>().SingleOrDefault(e => e.Id == 799 && e.ByteArray9000.SequenceEqual(longBinary)));
            }
        }
    }
}
