// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Abstractions;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     The Gears of War model under TPT and TPC, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>The largest of the TPT and TPC family, and the last.</b> This is EF's hardest query
///         model, and running it over a hierarchy split across store objects is the broadest
///         available statement that R7's mapping work holds under load rather than only on the
///         inheritance model. <b>3419 of 3529 passed on the first run, before a single override
///         was written.</b>
///     </para>
///     <para>
///         <b>Every override below was adopted AFTER measuring, never in advance.</b> EF's own
///         SQLite classes carry 23 each; only 15 were measured red here, and those 15 are what is
///         written. Adopting all 23 would have imported eight workarounds for limitations this
///         wire never reaches, which is the mistake CLAUDE.md records as an override outliving its
///         cause.
///     </para>
///     <para>
///         <b>Two more overrides per class were added in V12, and they are NOT EF's.</b> Both
///         classes used to leave a family red with "no exception was thrown": the relational base
///         asserts that a correlated collection with <c>Distinct</c> must be refused, and this
///         provider answers it, because the projection split reassembles on the client. Those two
///         now carry EF's <em>core</em> assertion instead — <c>AssertQuery</c>, row by row —
///         which is a stronger statement than the refusal it displaces, not a weaker one. See the
///         remarks on
///         <see cref="TPTGearsOfWarQueryInfoCarrierTest.Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions" />.
///     </para>
///     <para>
///         <b>The sibling <c>Correlated_collection_with_distinct_3_levels</c> is deliberately NOT
///         treated this way</b>, and it lives in <c>GearsOfWarQueryInfoCarrierTest</c> on Tier A.
///         C64 proved its assertion cannot be satisfied by any answer, so an override there would
///         be green because the assertion is broken. <c>docs/upstream-defects.md</c> §1.4 carries
///         it.
///     </para>
/// </remarks>
public class TPTGearsOfWarQueryInfoCarrierTest : TPTGearsOfWarQueryRelationalTestBase<TPTGearsOfWarQueryInfoCarrierFixture>
{
    public TPTGearsOfWarQueryInfoCarrierTest(
        TPTGearsOfWarQueryInfoCarrierFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own. EF asserts <c>SqliteException</c> with
    ///     <c>SQLite Error 1: 'no such column: s.Id'</c>; the measured message here is identical,
    ///     wrapped by the wire, so the assertion keeps the engine's own type name and text.
    /// </remarks>
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => GearsOfWarSqliteAssertions.StoreRefuses(() => base.Where_subquery_with_ElementAt_using_column_as_index(async));

    /// <summary>
    ///     Two queries EF's relational base asserts a refusal for, and this provider answers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Not EF's overrides, and they assert MORE rather than less.</b>
    ///         <c>GearsOfWarQueryRelationalTestBase</c> asserts
    ///         <c>RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin</c> for
    ///         both, because <c>Distinct</c> drops the columns that say which owner a projected
    ///         collection element belongs to, and a relational provider has to attribute rows after
    ///         a join. <b>This provider never builds that join</b>: the server returns rows and the
    ///         projection is reassembled on the client, so the query is answered and the rows are
    ///         right.
    ///     </para>
    ///     <para>
    ///         <b>Why this is not the override CLAUDE.md forbids.</b> That guardrail is about
    ///         suppressing a red test. Each of these replaces <em>"must throw"</em> with
    ///         <em>"must return exactly these rows"</em> — EF's core <c>AssertQuery</c>, checking
    ///         every row against the in-memory expected result — which fails if the answer ever
    ///         becomes wrong, where the refusal assertion would keep failing whatever the rows
    ///         were. <b>The query bodies are EF's own, copied</b>, because C# cannot call a
    ///         grandparent's implementation and the relational base sits between. The copy is the
    ///         real cost: an edit to EF's base will not reach it.
    ///     </para>
    ///     <para>
    ///         <b>Note what this does NOT do.</b> The sibling
    ///         <c>Correlated_collection_with_distinct_3_levels</c> stays red, and deliberately: C64
    ///         proved its assertion cannot be satisfied by <em>any</em> answer, so an override there
    ///         would be green because the assertion is broken. See
    ///         <c>docs/upstream-defects.md</c> §1.4 and <c>implementation-plan.md</c> V12.
    ///     </para>
    /// </remarks>
    public override Task Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions(
        bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Gear>()
                .Select(g => new
                {
                    Key = g.Nickname,
                    Subquery = g.Weapons
                        .Select(w => new { w.Name, w.IsAutomatic, w.OwnerFullName!.Length })
                        .Distinct().ToList()
                }),
            elementSorter: e => e.Key,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.Key, a.Key);
                AssertCollection(
                    e.Subquery,
                    a.Subquery,
                    elementSorter: ee => ee.Name,
                    elementAsserter: (ee, aa) =>
                    {
                        Assert.Equal(ee.Name, aa.Name);
                        Assert.Equal(ee.IsAutomatic, aa.IsAutomatic);
                        Assert.Equal(ee.Length, aa.Length);
                    });
            });

    /// <inheritdoc cref="Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions" />
    public override Task Correlated_collection_after_distinct_3_levels_without_original_identifiers(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Squad>()
                .Select(s => new { s.Name.Length })
                .Distinct()
                .Select(x => new
                {
                    x.Length,
                    Subquery1 = (from g in ss.Set<Gear>()
                                 where g.Nickname.Length == x.Length
                                 select new { g.HasSoulPatch, g.CityOfBirthName })
                        .Distinct()
                        .Select(xx => new
                        {
                            xx.HasSoulPatch,
                            Subquery2 = (from w in ss.Set<Weapon>()
                                         where w.OwnerFullName == xx.CityOfBirthName
                                         select new
                                         {
                                             w.Id,
                                             x.Length,
                                             xx.HasSoulPatch
                                         }).ToList()
                        })
                        .ToList()
                }),
            elementSorter: e => e.Length,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.Length, a.Length);
                AssertCollection(
                    e.Subquery1,
                    a.Subquery1,
                    elementSorter: ee => ee.HasSoulPatch,
                    elementAsserter: (ee, aa) =>
                    {
                        Assert.Equal(ee.HasSoulPatch, aa.HasSoulPatch);
                        AssertCollection(
                            ee.Subquery2,
                            aa.Subquery2,
                            elementSorter: eee => eee.Id,
                            elementAsserter: (eee, aaa) =>
                            {
                                Assert.Equal(eee.Id, aaa.Id);
                                Assert.Equal(eee.Length, aaa.Length);
                                Assert.Equal(eee.HasSoulPatch, aaa.HasSoulPatch);
                            });
                    });
            });
}

/// <inheritdoc cref="TPTGearsOfWarQueryInfoCarrierTest" />
public class TPCGearsOfWarQueryInfoCarrierTest : TPCGearsOfWarQueryRelationalTestBase<TPCGearsOfWarQueryInfoCarrierFixture>
{
    public TPCGearsOfWarQueryInfoCarrierTest(
        TPCGearsOfWarQueryInfoCarrierFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own. EF asserts <c>SqliteException</c> with
    ///     <c>SQLite Error 1: 'no such column: s.Id'</c>; the measured message here is identical,
    ///     wrapped by the wire, so the assertion keeps the engine's own type name and text.
    /// </remarks>
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => GearsOfWarSqliteAssertions.StoreRefuses(() => base.Where_subquery_with_ElementAt_using_column_as_index(async));

    /// <summary>
    ///     TPC's copy of the two overrides described on <see cref="TPTGearsOfWarQueryInfoCarrierTest" />:
    ///     EF's relational base asserts a refusal, this provider answers, and the assertion is
    ///     replaced by EF's own row-by-row one rather than removed.
    /// </summary>
    public override Task Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions(
        bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Gear>()
                .Select(g => new
                {
                    Key = g.Nickname,
                    Subquery = g.Weapons
                        .Select(w => new { w.Name, w.IsAutomatic, w.OwnerFullName!.Length })
                        .Distinct().ToList()
                }),
            elementSorter: e => e.Key,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.Key, a.Key);
                AssertCollection(
                    e.Subquery,
                    a.Subquery,
                    elementSorter: ee => ee.Name,
                    elementAsserter: (ee, aa) =>
                    {
                        Assert.Equal(ee.Name, aa.Name);
                        Assert.Equal(ee.IsAutomatic, aa.IsAutomatic);
                        Assert.Equal(ee.Length, aa.Length);
                    });
            });

    /// <inheritdoc cref="Correlated_collection_with_distinct_not_projecting_identifier_column_also_projecting_complex_expressions" />
    public override Task Correlated_collection_after_distinct_3_levels_without_original_identifiers(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<Squad>()
                .Select(s => new { s.Name.Length })
                .Distinct()
                .Select(x => new
                {
                    x.Length,
                    Subquery1 = (from g in ss.Set<Gear>()
                                 where g.Nickname.Length == x.Length
                                 select new { g.HasSoulPatch, g.CityOfBirthName })
                        .Distinct()
                        .Select(xx => new
                        {
                            xx.HasSoulPatch,
                            Subquery2 = (from w in ss.Set<Weapon>()
                                         where w.OwnerFullName == xx.CityOfBirthName
                                         select new
                                         {
                                             w.Id,
                                             x.Length,
                                             xx.HasSoulPatch
                                         }).ToList()
                        })
                        .ToList()
                }),
            elementSorter: e => e.Length,
            elementAsserter: (e, a) =>
            {
                Assert.Equal(e.Length, a.Length);
                AssertCollection(
                    e.Subquery1,
                    a.Subquery1,
                    elementSorter: ee => ee.HasSoulPatch,
                    elementAsserter: (ee, aa) =>
                    {
                        Assert.Equal(ee.HasSoulPatch, aa.HasSoulPatch);
                        AssertCollection(
                            ee.Subquery2,
                            aa.Subquery2,
                            elementSorter: eee => eee.Id,
                            elementAsserter: (eee, aaa) =>
                            {
                                Assert.Equal(eee.Id, aaa.Id);
                                Assert.Equal(eee.Length, aaa.Length);
                                Assert.Equal(eee.HasSoulPatch, aaa.HasSoulPatch);
                            });
                    });
            });
}

/// <summary>
///     The two assertions the SQLite overrides above share. EF's TPT and TPC SQLite classes carry
///     byte-identical override sets, so stating them once is the honest shape.
/// </summary>
internal static class GearsOfWarSqliteAssertions
{
    internal static async Task ApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    internal static async Task StoreRefuses(Func<Task> query)
    {
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(query);

        Assert.Equal(typeof(SqliteException).FullName, exception.ServerExceptionTypeName);
        Assert.Contains("no such column", exception.Message);
    }
}

/// <summary>
///     The TPT Gears of War fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPTGearsOfWarQueryInfoCarrierFixture : TPTGearsOfWarQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}

/// <summary>
///     The TPC Gears of War fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class TPCGearsOfWarQueryInfoCarrierFixture : TPCGearsOfWarQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
