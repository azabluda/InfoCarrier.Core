// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>BuiltInDataTypesTestBase</c> on ADR-009 Tier B.
/// </summary>
/// <remarks>
///     Every primitive the CLR has, written and read back. That is the wire format's own subject —
///     <c>PrimitiveCoercion</c> decides how each one travels — and a base that round-trips all of
///     them at once is the most direct check of it there is.
///     <para>
///         The fixture's capability flags and the one override are EF's own
///         <c>BuiltInDataTypesInMemoryTest</c>'s, because they describe the backing store.
///     </para>
/// </remarks>
public class BuiltInDataTypesInfoCarrierTest(BuiltInDataTypesInfoCarrierTest.BuiltInDataTypesInfoCarrierFixture fixture)
    : BuiltInDataTypesTestBase<BuiltInDataTypesInfoCarrierTest.BuiltInDataTypesInfoCarrierFixture>(fixture)
{
    public class BuiltInDataTypesInfoCarrierFixture : BuiltInDataTypesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "BuiltInDataTypesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override bool StrictEquality => false;

        public override bool SupportsAnsi => false;

        public override bool SupportsUnicodeToAnsiConversion => true;

        public override bool SupportsLargeStringComparisons => true;

        public override bool SupportsBinaryKeys => true;

        public override bool SupportsDecimalComparisons => false;

        public override DateTime DefaultDateTime => new();

        public override bool PreservesDateTimeKind => false;
    }
}
