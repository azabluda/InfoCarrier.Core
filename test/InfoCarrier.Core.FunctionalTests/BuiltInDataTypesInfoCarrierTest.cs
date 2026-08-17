// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>BuiltInDataTypesTestBase</c> on ADR-009 Tier A.
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
    /// <inheritdoc />
    /// <remarks>The InMemory store has no null to read: it stores CLR values as they are.</remarks>
    public override Task Optional_datetime_reading_null_from_database()
        => Task.CompletedTask;

    public class BuiltInDataTypesInfoCarrierFixture : BuiltInDataTypesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "BuiltInDataTypesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override bool StrictEquality => true;

        public override bool SupportsAnsi => false;

        public override bool SupportsUnicodeToAnsiConversion => true;

        public override bool SupportsLargeStringComparisons => true;

        public override bool SupportsBinaryKeys => false;

        public override bool SupportsDecimalComparisons => true;

        public override DateTime DefaultDateTime => new();

        public override bool PreservesDateTimeKind => true;
    }
}

/// <summary>
///     <c>ConvertToProviderTypesTestBase</c> on Tier A — the same corpus with every property
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
    /// <inheritdoc />
    public override Task Optional_datetime_reading_null_from_database()
        => Task.CompletedTask;

    public class ConvertToProviderTypesInfoCarrierFixture : ConvertToProviderTypesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ConvertToProviderTypesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override bool StrictEquality => true;

        public override bool SupportsAnsi => false;

        public override bool SupportsUnicodeToAnsiConversion => true;

        public override bool SupportsLargeStringComparisons => true;

        public override bool SupportsBinaryKeys => false;

        public override bool SupportsDecimalComparisons => true;

        public override DateTime DefaultDateTime => new();

        public override bool PreservesDateTimeKind => true;
    }
}

/// <summary>
///     <c>CustomConvertersTestBase</c> on ADR-009 Tier B — user-written converters rather than the
///     provider's own.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tier B, and it was Tier A until J2.</b> Tier A brought <b>four</b> skips with it —
///         EF issue #17050, from <c>CustomConvertersInMemoryTest</c> — and each one is a
///         <em>collection</em> property behind a converter, which is the shape B4 records as the
///         most dangerous thing this wire carries. <c>CustomConvertersSqliteTest</c> skips
///         <b>none</b> of them. It also drops two more InMemory statements: the store is no longer
///         case-sensitive by construction, and a non-composed <c>GroupBy</c> is no longer refused.
///     </para>
///     <para>
///         The fixture's capability flags are <c>CustomConvertersSqliteFixture</c>'s, because they
///         describe the backing store and the backing store is now SQLite. Three of the eight
///         change value, and they are not cosmetic: <c>StrictEquality</c> and
///         <c>SupportsDecimalComparisons</c> become <c>false</c> and <c>SupportsBinaryKeys</c>
///         becomes <c>true</c>, which turns assertions on and off inside the base.
///     </para>
///     <para>
///         EF's nine <c>AssertSql</c> overrides are deliberately <em>not</em> taken: they exist to
///         pin generated SQL, which is the backend's business and not observable here. Its two
///         behavioural overrides are.
///     </para>
/// </remarks>
public class CustomConvertersInfoCarrierTest(CustomConvertersInfoCarrierTest.CustomConvertersInfoCarrierFixture fixture)
    : CustomConvertersTestBase<CustomConvertersInfoCarrierTest.CustomConvertersInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     <c>CustomConvertersSqliteTest</c>'s, unchanged in substance — SQLite has no
    ///     case-insensitive comparison for this key either. Only the reason moves: it was
    ///     "the InMemory store is case-sensitive" and it is now the reference provider's own
    ///     override for the same test.
    /// </remarks>
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => Task.CompletedTask;

    // `Value_conversion_on_enum_collection_contains` is EF's other behavioural SQLite override and
    // is deliberately NOT taken. Adopting it measured `Assert.Throws() Failure: No exception was
    // thrown`: the query this provider ships is answered rather than refused, so EF's assertion
    // that SQLite cannot translate it is not true of this arrangement. An override that measurement
    // disproves is a workaround, and CLAUDE.md says to delete it rather than keep it for symmetry.

    public class CustomConvertersInfoCarrierFixture : CustomConvertersFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "CustomConvertersInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
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

        public override bool PreservesDateTimeKind => true;
    }
}
