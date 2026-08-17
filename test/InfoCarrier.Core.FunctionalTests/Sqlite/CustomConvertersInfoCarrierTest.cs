// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
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
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => Task.CompletedTask;

    /// <summary>
    ///     Runs the query of <see cref="Composition_over_collection_of_complex_mapped_as_scalar" />
    ///     against seeded data and asserts the <em>answer</em>, which that test never could.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The base test is an <c>Assert.Throws</c> over an empty table.</b> EF's fixture
    ///         seeds no <c>Dashboard</c> at all — the base only ever asserted that the query is
    ///         refused, so EF never needed a row. A probe measured
    ///         <c>rows=0 expectedRows=0 REMOTED=(empty) EXPECTED=(empty)</c>, which means "no
    ///         exception was thrown" was being observed over nothing and said nothing about
    ///         whether this provider's answer is right. Classifying it as A28 — a spec test
    ///         asserting a limitation this provider does not have — requires evidence that the
    ///         answer <em>is</em> right, and there was no answer to check.
    ///     </para>
    ///     <para>
    ///         <see cref="CustomConvertersInfoCarrierFixture.SeedAsync" /> now seeds two
    ///         dashboards with <b>different numbers of layouts</b> and no two integers alike, so
    ///         four distinct wrong answers are distinguishable from the right one: a row lost, the
    ///         layouts of one row given to the other, a truncated list, and <c>H</c>/<c>W</c>
    ///         transposed (the serializer writes <c>(Height,Width)</c>, so a transposition is
    ///         silent unless the two differ). That is the non-vacuity bar
    ///         <c>Collection_enum_as_string_Contains</c> was held to in J2.
    ///     </para>
    ///     <para>
    ///         The query body is copied byte-for-byte from the base and <b>not</b> ordered inside
    ///         the query — ordering is applied to the materialized result — so what crosses the
    ///         wire is exactly the tree the base builds.
    ///     </para>
    /// </remarks>
    [ConditionalFact]
    public virtual void Composition_over_collection_of_complex_mapped_as_scalar_returns_the_right_answer()
    {
        using DbContext context = CreateContext();

        var result = context.Set<Dashboard>().AsNoTracking().Select(d => new
        {
            d.Id,
            d.Name,
            Layouts = d.Layouts.Select(l => new { H = l.Height, W = l.Width }).ToList()
        }).ToList();

        Assert.Collection(
            result.OrderBy(r => r.Id),
            first =>
            {
                Assert.Equal(CompositionSeedFirstId, first.Id);
                Assert.Equal("Dashboard one", first.Name);
                Assert.Equal([(11, 12), (13, 14)], first.Layouts.Select(l => (l.H, l.W)));
            },
            second =>
            {
                Assert.Equal(CompositionSeedSecondId, second.Id);
                Assert.Equal("Dashboard two", second.Name);
                Assert.Equal([(21, 22), (23, 24), (25, 26)], second.Layouts.Select(l => (l.H, l.W)));
            });
    }

    private const int CompositionSeedFirstId = 4001;

    private const int CompositionSeedSecondId = 4002;

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

        /// <summary>
        ///     Seeds the <c>Dashboard</c> set EF's own fixture leaves empty.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Nothing in <c>CustomConvertersTestBase</c> reads <c>Dashboard</c> except
        ///         <see cref="Composition_over_collection_of_complex_mapped_as_scalar" /> and the
        ///         test above, so this adds data to one otherwise-unused set and can change no
        ///         other result. It runs against the <b>server</b> context — <see cref="TestStore" />
        ///         hands the seed the backend's context, not the client's — so the rows are written
        ///         through the backing store's own model and the converter, and the read side is
        ///         then the only thing under test.
        ///     </para>
        ///     <para>
        ///         The base test still fails, and its message is unchanged: it asserts a throw, and
        ///         seeding cannot make a query that answers start refusing. What seeding changes is
        ///         that the <em>answer</em> is now observable.
        ///     </para>
        /// </remarks>
        protected override async Task SeedAsync(PoolableDbContext context)
        {
            await base.SeedAsync(context);

            context.Set<Dashboard>().AddRange(
                new Dashboard
                {
                    Id = CompositionSeedFirstId,
                    Name = "Dashboard one",
                    Layouts =
                    [
                        new Layout { Height = 11, Width = 12 },
                        new Layout { Height = 13, Width = 14 },
                    ],
                },
                new Dashboard
                {
                    Id = CompositionSeedSecondId,
                    Name = "Dashboard two",
                    Layouts =
                    [
                        new Layout { Height = 21, Width = 22 },
                        new Layout { Height = 23, Width = 24 },
                        new Layout { Height = 25, Width = 26 },
                    ],
                });

            await context.SaveChangesAsync();
        }
    }
}
