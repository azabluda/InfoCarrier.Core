// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
///         <b>22 since 2026-09-22.</b> Seven more came red with EF's own <c>ApplyNotSupported</c>, and
///         until then this provider answered each of them without anything here recording it. The
///         two <c>Correlated_collection_with_distinct_*_identifier_column</c> tests ran their
///         <c>Distinct</c> on the client. The four whose inner collection reads an outer element
///         (<c>*_references_outer_qsre*</c>, <c>*_references_element_two_levels_up</c>) carried that
///         read outside the collection, so the server never needed <c>APPLY</c>.
///         <c>Correlated_collection_after_distinct_3_levels</c> ran every projection after its
///         first <c>Distinct</c> on the client.
///     </para>
///     <para>
///         <b>26 since 2026-09-27</b>, in each of the three classes: the four
///         <c>Correlated_collection_with_groupby_*</c> tests came red with EF's own
///         <c>ApplyNotSupported</c> once a grouping on an anonymous key, projected into a collection,
///         stopped running on the client. Until then this provider answered them, and #167's slow
///         run had them red for it.
///     </para>
///     <para>
///         <b>No override here is this provider's own since 2026-09-22.</b> Two per class used to
///         replace the relational base's refusal of a correlated collection after <c>Distinct</c>
///         with the core base's row-by-row answer, because the projection split reassembled that
///         collection on the client. <c>ProjectionRewriter</c> now moves such a <c>Distinct</c>
///         onto the server and fuses a projection with the rebuild below it, so the server's EF
///         refuses both queries with EF's own message and both tests inherit EF's assertion.
///     </para>
///     <para>
///         <b>The TPH class below inherits the same refusals since 2026-09-27</b>, when it moved
///         from Tier A. Until then this paragraph read: "The Tier A
///         <c>Correlated_collection_with_distinct_3_levels</c> is a different matter, and it lives in
///         <c>GearsOfWarQueryInfoCarrierTest</c>. C64 proved its assertion cannot be satisfied by any
///         answer." That is still true of the base's assertion (<c>docs/upstream-defects.md</c>
///         §1.4), and the relational base refuses the query before the assertion runs.
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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 21, 22,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 18, 19,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 24, 25,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 144, 149,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 27, 31,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 156, 159,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 51, 55,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 57, 60,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 62, 65,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 95, 99,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 79, 85,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 87, 93,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 67, 71,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 73, 77,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own. EF asserts <c>SqliteException</c> with
    ///     <c>SQLite Error 1: 'no such column: s.Id'</c>; the measured message here is identical,
    ///     wrapped by the wire, so the assertion keeps the engine's own type name and text.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 181, 199,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => GearsOfWarSqliteAssertions.StoreRefuses(() => base.Where_subquery_with_ElementAt_using_column_as_index(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-22, when the <c>Distinct</c> stayed on the client; see
    ///     <c>ProjectionRewriter.TryMoveDistinctBelowReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 138, 142,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_not_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_not_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-22, when the <c>Distinct</c> stayed on the client; see
    ///     <c>ProjectionRewriter.TryMoveDistinctBelowReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 132, 136,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 101, 105,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_inner_collection_references_element_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_inner_collection_references_element_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 33, 37,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_selector_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_selector_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 39, 43,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 45, 49,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: a correlated collection after <c>Distinct</c> needs <c>APPLY</c>. It answered
    ///     here until 2026-09-22, when every projection after the first <c>Distinct</c> ran on the
    ///     client; see <c>ProjectionRewriter.TryFuseSelectWithReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 151, 154,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_after_distinct_3_levels(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_after_distinct_3_levels(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 124, 130,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 107, 113,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 115, 122,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPTGearsOfWarQuerySqliteTest.cs", 161, 168,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));
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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 21, 22,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 18, 19,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 24, 25,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 144, 149,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 27, 31,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 156, 159,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 51, 55,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 57, 60,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 62, 65,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 95, 99,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 79, 85,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 87, 93,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 67, 71,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 73, 77,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own. EF asserts <c>SqliteException</c> with
    ///     <c>SQLite Error 1: 'no such column: s.Id'</c>; the measured message here is identical,
    ///     wrapped by the wire, so the assertion keeps the engine's own type name and text.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 181, 205,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => GearsOfWarSqliteAssertions.StoreRefuses(() => base.Where_subquery_with_ElementAt_using_column_as_index(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-22, when the <c>Distinct</c> stayed on the client; see
    ///     <c>ProjectionRewriter.TryMoveDistinctBelowReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 138, 142,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_not_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_not_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-22, when the <c>Distinct</c> stayed on the client; see
    ///     <c>ProjectionRewriter.TryMoveDistinctBelowReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 132, 136,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 101, 105,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_inner_collection_references_element_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_inner_collection_references_element_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 33, 37,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_selector_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_selector_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 39, 43,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: the inner collection reads an outer element, which needs <c>APPLY</c>. It
    ///     answered here until 2026-09-22, when that read was carried outside the collection;
    ///     see <c>ProjectionRewriter._enclosing</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 45, 49,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: a correlated collection after <c>Distinct</c> needs <c>APPLY</c>. It answered
    ///     here until 2026-09-22, when every projection after the first <c>Distinct</c> ran on the
    ///     client; see <c>ProjectionRewriter.TryFuseSelectWithReassembly</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 151, 154,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_after_distinct_3_levels(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_after_distinct_3_levels(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 124, 130,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 107, 113,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 115, 122,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Inheritance/TPCGearsOfWarQuerySqliteTest.cs", 161, 168,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));
}

/// <summary>
///     The Gears of War model under TPH, on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request.</b> Tier A's store is EF's
///         InMemory provider, which runs no statement, so #167's slow run could not compare this base
///         with plain EF; its TPT and TPC siblings above showed how much that comparison finds. The
///         Tier A class carried EF's own <c>GearsOfWarQueryInMemoryTest</c> overrides as a set,
///         and every one of them described the InMemory store, so none came along. Nor did the one
///         test that class added, <c>Select_ToString_on_non_nullable_property_of_an_optional_entity</c>:
///         EF's InMemory class declares it and EF's SQLite class does not.
///     </para>
///     <para>
///         <b>Every override below was adopted after measuring</b>, as on the siblings.
///     </para>
/// </remarks>
public class GearsOfWarQueryInfoCarrierTest : GearsOfWarQueryRelationalTestBase<GearsOfWarQueryInfoCarrierFixture>
{
    public GearsOfWarQueryInfoCarrierTest(
        GearsOfWarQueryInfoCarrierFixture fixture,
        ITestOutputHelper testOutputHelper)
        : base(fixture)
    {
        Fixture.TestSqlLoggerFactory.Clear();
        Fixture.TestSqlLoggerFactory.SetTestOutputHelper(testOutputHelper);
    }

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 40, 41,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 33, 38,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 43, 48,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 239, 248,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 50, 58,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 266, 273,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 90, 98,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 100, 107,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 109, 116,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 162, 170,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 138, 148,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 150, 160,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 118, 126,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 128, 136,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own. EF asserts <c>SqliteException</c> with
    ///     <c>SQLite Error 1: 'no such column: s.Id'</c>; the measured message here is identical,
    ///     wrapped by the wire, so the assertion keeps the engine's own type name and text.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 8657, 8675,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => GearsOfWarSqliteAssertions.StoreRefuses(() => base.Where_subquery_with_ElementAt_using_column_as_index(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 229, 237,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_not_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_not_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 219, 227,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_distinct_projecting_identifier_column(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_distinct_projecting_identifier_column(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 172, 180,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_inner_collection_references_element_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_inner_collection_references_element_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 60, 68,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_selector_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_selector_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 70, 78,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_one_level_up(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 80, 88,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_nested_inner_subquery_references_outer_qsre_two_levels_up(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 257, 264,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_after_distinct_3_levels(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_after_distinct_3_levels(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 207, 217,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_but_only_grouping_key_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 182, 192,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 194, 205,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_not_projecting_identifier_column_with_group_aggregate_in_final_projection_multiple_grouping_keys(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own: this shape needs <c>APPLY</c>, which SQLite does not have. It answered here
    ///     until 2026-09-27, when the grouping on an anonymous key ran on the client; see
    ///     <c>TransparentIdentifierRewriter.Rewriter.RebuildEach</c>.
    /// </remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/GearsOfWarQuerySqliteTest.cs", 8081, 8092,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_with_groupby_with_complex_grouping_key_not_projecting_identifier_column_with_group_aggregate_in_final_projection(async));
}

/// <summary>
///     The two assertions the SQLite overrides above share. EF's TPH, TPT and TPC SQLite classes
///     carry the same failing override sets, so stating them once is the honest shape.
/// </summary>
internal static class GearsOfWarSqliteAssertions
{
    internal static async Task ApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);


    internal static Task StoreRefuses(Func<Task> query)
        => SqliteStoreRefusal.AssertAsync(query, "no such column");
}

/// <summary>
///     The TPH Gears of War fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class GearsOfWarQueryInfoCarrierFixture : GearsOfWarQueryRelationalFixture
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
