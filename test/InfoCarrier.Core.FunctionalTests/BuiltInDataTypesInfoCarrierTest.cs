// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

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
///     <c>CustomConvertersTestBase</c> on Tier A — user-written converters rather than the
///     provider's own.
/// </summary>
/// <remarks>
///     The overrides are EF's <c>CustomConvertersInMemoryTest</c>'s, one for one: a case-sensitive
///     store, EF issue #17050, and the non-composed <c>GroupBy</c> the backing store cannot do.
/// </remarks>
public class CustomConvertersInfoCarrierTest(CustomConvertersInfoCarrierTest.CustomConvertersInfoCarrierFixture fixture)
    : CustomConvertersTestBase<CustomConvertersInfoCarrierTest.CustomConvertersInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Optional_datetime_reading_null_from_database()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>The InMemory store is case-sensitive.</remarks>
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => Task.CompletedTask;

    [ConditionalFact(Skip = "Issue#17050")]
    public override void Value_conversion_with_property_named_value()
    {
    }

    [ConditionalFact(Skip = "Issue#17050")]
    public override void Collection_property_as_scalar_Any()
        => base.Collection_property_as_scalar_Any();

    [ConditionalFact(Skip = "Issue#17050")]
    public override void Collection_property_as_scalar_Count_member()
        => base.Collection_property_as_scalar_Count_member();

    [ConditionalFact(Skip = "Issue#17050")]
    public override void Collection_enum_as_string_Contains()
        => base.Collection_enum_as_string_Contains();

    /// <inheritdoc />
    /// <remarks>A non-composed <c>GroupBy</c> is a backing-store limitation, and the store is InMemory.</remarks>
    public override void GroupBy_converted_enum()
        => Assert.Contains(
            CoreStrings.TranslationFailedWithDetails("", InMemoryStrings.NonComposedGroupByNotSupported)[21..],
            Assert.Throws<InvalidOperationException>(base.GroupBy_converted_enum).Message);

    public class CustomConvertersInfoCarrierFixture : CustomConvertersFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "CustomConvertersInfoCarrierTest";

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
