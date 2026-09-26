// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
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
///         <b>No override here is this provider's own since 2026-09-22.</b> Two per class used to
///         replace the relational base's refusal of a correlated collection after <c>Distinct</c>
///         with the core base's row-by-row answer, because the projection split reassembled that
///         collection on the client. <c>ProjectionRewriter</c> now moves such a <c>Distinct</c>
///         onto the server and fuses a projection with the rebuild below it, so the server's EF
///         refuses both queries with EF's own message and both tests inherit EF's assertion.
///     </para>
///     <para>
///         <b>The Tier A <c>Correlated_collection_with_distinct_3_levels</c> is a different
///         matter</b>, and it lives in <c>GearsOfWarQueryInfoCarrierTest</c>. C64 proved its
///         assertion cannot be satisfied by any answer. <c>docs/upstream-defects.md</c> §1.4
///         carries it.
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 23, 24,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 20, 21,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 26, 27,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 146, 151,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 29, 33,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 158, 161,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 53, 57,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 59, 62,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 64, 67,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 97, 101,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 81, 87,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 89, 95,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 69, 73,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 75, 79,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 183, 201,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 140, 144,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 134, 138,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 103, 107,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 35, 39,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 41, 45,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 47, 51,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPTGearsOfWarQuerySqliteTest.cs", 153, 156,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_after_distinct_3_levels(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_after_distinct_3_levels(async));
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 23, 24,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffsetNow_minus_timespan(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffsetNow_minus_timespan(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 20, 21,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Contains_Less_than_Greater_than(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Contains_Less_than_Greater_than(async));

    /// <inheritdoc />
    /// <remarks>EF's own: SQLite has no <c>DateTimeOffset</c> type, so this cannot translate.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 26, 27,
        Justification = Upstream.GaveNoReason)]
    public override Task DateTimeOffset_Date_returns_datetime(bool async)
        => AssertTranslationFailed(() => base.DateTimeOffset_Date_returns_datetime(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 146, 151,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_via_SelectMany_with_Distinct_missing_indentifying_columns_in_projection(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 29, 33,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_inner_subquery_predicate_references_outer_qsre(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_inner_subquery_predicate_references_outer_qsre(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 158, 161,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collections_with_Distinct(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collections_with_Distinct(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 53, 57,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_group_join_with_DefaultIfEmpty(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_group_join_with_DefaultIfEmpty(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 59, 62,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 64, 67,
        Justification = Upstream.GaveNoReason)]
    public override Task Outer_parameter_in_join_key_inner_and_outer(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Outer_parameter_in_join_key_inner_and_outer(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 97, 101,
        Justification = Upstream.GaveNoReason)]
    public override Task SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.SelectMany_predicate_with_non_equality_comparison_with_Take_doesnt_convert_to_join(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 81, 87,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 89, 95,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_non_nullable_scalar_contains_non_nullable_value_doesnt_need_null_expansion_negated(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 69, 73,
        Justification = Upstream.GaveNoReason)]
    public override Task Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Subquery_projecting_nullable_scalar_contains_nullable_value_needs_null_expansion(async));

    /// <inheritdoc />
    /// <remarks>EF's own: this shape needs <c>APPLY</c>, which SQLite does not have.</remarks>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 75, 79,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 183, 207,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 140, 144,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 134, 138,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 103, 107,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 35, 39,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 41, 45,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 47, 51,
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
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/TPCGearsOfWarQuerySqliteTest.cs", 153, 156,
        Justification = Upstream.GaveNoReason)]
    public override Task Correlated_collection_after_distinct_3_levels(bool async)
        => GearsOfWarSqliteAssertions.ApplyNotSupported(() => base.Correlated_collection_after_distinct_3_levels(async));
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


    internal static Task StoreRefuses(Func<Task> query)
        => SqliteStoreRefusal.AssertAsync(query, "no such column");
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
