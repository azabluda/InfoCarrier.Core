// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;


/// <summary>
///     <c>ConvertToProviderTypesTestBase</c> on ADR-009 Tier B — the same corpus with every property
///     converted to a provider type on the way out.
/// </summary>
/// <remarks>
///     A19's rule — a converted value travels as its <em>provider</em> value — applied to every
///     primitive at once rather than to the one property that exposed it.
/// </remarks>
public class ConvertToProviderTypesInfoCarrierTest(
    ConvertToProviderTypesInfoCarrierTest.ConvertToProviderTypesInfoCarrierFixture fixture)
    : ConvertToProviderTypesTestBase<
        ConvertToProviderTypesInfoCarrierTest.ConvertToProviderTypesInfoCarrierFixture>(fixture)
{
    public class ConvertToProviderTypesInfoCarrierFixture : ConvertToProviderTypesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ConvertToProviderTypesInfoCarrierTest";

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
