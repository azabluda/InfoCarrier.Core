// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/CustomConvertersSqliteTest.cs", 15, 16,
        Justification = "Disabled: SQLite database is case-sensitive",
        Skip = true)]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <summary>
    ///     <c>CustomConvertersSqliteTest</c>'s own override, adopted verbatim once R72 let the
    ///     query reach the store.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>This test used to pass here, and passing was the wrong answer.</b>
    ///         <c>MessageGroup</c> is an enum nested in the generic
    ///         <c>CustomConvertersTestBase&lt;TFixture&gt;</c>, so R72's allowlist defect refused
    ///         it, the boundary analyzer refused the <c>Contains</c> with it, and the projection
    ///         split evaluated the whole <c>Where</c> on the client — which answers a query
    ///         <b>SQLite cannot translate</b>. EF's own SQLite class overrides this test to assert
    ///         exactly that refusal, which is CLAUDE.md's standing test for a newly-red SQLite
    ///         test: the query now reaches SQL, and this is convergence with the reference
    ///         provider rather than a regression.
    ///     </para>
    ///     <para>
    ///         Its sibling <c>Collection_enum_as_string_Contains</c> moved the other way in the
    ///         same run and needs no override: it asserts the refusal, this provider now raises
    ///         it, and it goes green.
    ///     </para>
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/CustomConvertersSqliteTest.cs", 131, 134,
        Justification = Upstream.GaveNoReason)]
    public override void Value_conversion_on_enum_collection_contains()
        => Assert.Contains(
            CoreStrings.TranslationFailed("")[47..],
            Assert.Throws<InvalidOperationException>(() => base.Value_conversion_on_enum_collection_contains()).Message);

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>The server's EF refuses it, as EF does, since 2026-09-22.</b> Until then the server
    ///         read the whole <c>Layouts</c> column and the client ran the inner <c>Select</c>, which
    ///         answered a query every EF provider refuses. The lambda over an element names
    ///         <c>Layout</c>, which the model does not imply, so the fixture registers it.
    ///         <c>ServerParameterizationTest</c> compares the two refusals directly, and pins what an
    ///         unregistered element type does.
    ///     </para>
    ///     <para>
    ///         The message names the tuple that carries the anonymous type, where EF names the
    ///         anonymous type.
    ///     </para>
    /// </remarks>
    [InfoCarrierDesign(
        10,
        Justification = "The anonymous type in the inner Select crosses the wire as a ValueTuple, so the server's EF "
            + "refuses the lambda it received and prints the tuple.",
        Deviation = DeviationKind.QueryWrittenOut | DeviationKind.Other,
        DeviationNote = "The same refusal of the same lambda, printed with the tuple. The base asserts the message inside "
            + "its own Assert.Throws, so its query is written out.")]
    public override void Composition_over_collection_of_complex_mapped_as_scalar()
    {
        using DbContext context = CreateContext();

        Assert.Equal(
            CoreStrings.TranslationFailed("l => new ValueTuple<int, int>(    Item1 = l.Height,     Item2 = l.Width)"),
            Assert.Throws<InvalidOperationException>(
                    () => context.Set<Dashboard>().AsNoTracking().Select(d => new
                    {
                        d.Id,
                        d.Name,
                        Layouts = d.Layouts.Select(l => new { H = l.Height, W = l.Width }).ToList()
                    }).ToList())
                .Message.Replace("\r", string.Empty).Replace("\n", string.Empty));
    }

    // REVERSED BY R72, and the reason the earlier measurement disproved it is now known.
    // `Value_conversion_on_enum_collection_contains` -- EF's other behavioural SQLite override --
    // used to be left out of this class, because adopting it measured `Assert.Throws() Failure: No
    // exception was thrown`. That reading was right about the evidence and wrong about the
    // mechanism: the query was answered rather than refused only because `TypeAllowlist` denied
    // `MessageGroup`, an enum nested in a generic type, so the `Contains` never reached the
    // boundary as shippable. With the allowlist fixed the query reaches SQLite, SQLite refuses it
    // exactly as EF says it does, and the override is adopted above. An override measurement
    // disproves is still a workaround to delete -- what changed is that measurement.

    public class CustomConvertersInfoCarrierFixture : CustomConvertersFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "CustomConvertersInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions,
                // The element of `Dashboard.Layouts`, a list stored in one column. Unregistered, the
                // inner `Select` of `Composition_over_collection_of_complex_mapped_as_scalar` stays on
                // this client, which answers a query EF refuses. The model does not imply the type,
                // by the owner's decision of 2026-09-22 (`security-review.md` §2b), so the fixture
                // names it as an application would.
                allowedTypes: [typeof(Layout)]);

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
