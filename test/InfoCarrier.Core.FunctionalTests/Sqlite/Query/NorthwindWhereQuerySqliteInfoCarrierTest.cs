// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <see cref="NorthwindWhereQueryRelationalTestBase{TFixture}" /> on ADR-009 Tier B (SQLite).
/// </summary>
/// <remarks>
///     <para>
///         Derives from the <em>relational</em> base (#56), not the core one. The relational base
///         adds one test the core base lacks —
///         <c>EF_MultipleParameters_with_non_evaluatable_argument_throws</c> (+2, sync and async)
///         — swaps in <c>RelationalQueryAsserter</c>, and turns
///         <c>Where_bool_client_side_negated</c> into an <c>AssertTranslationFailed</c>, which is
///         why that override is no longer declared here: it is inherited.
///     </para>
///     <para>
///         The other overrides below are this provider's own, added only for what a run actually
///         showed. Red here is information (CLAUDE.md), not a regression.
///     </para>
/// </remarks>
public class NorthwindWhereQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindWhereQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    // -------------------------------------------------------------------------------------
    // UPSTREAM EF CORE LIMITATION — anonymous-type / tuple structural equality against a
    // constant, EF Core issue #14672. EF's own NorthwindWhereQuerySqliteTest overrides these
    // eight with AssertTranslationFailed; measured here, six of the eight fail the same way for
    // the same reason. Two of them used to return zero rows where six were expected — a
    // silent wrong answer — which is the defect the reference-equality guard fixed.
    //
    // Tier A no-ops these instead, because InMemory silently matches nothing where a relational
    // provider reports a translation failure. That difference in *shape* was predicted in the
    // Tier A class and is now confirmed rather than assumed.
    // -------------------------------------------------------------------------------------

    public override Task Where_compare_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

    public override Task Where_compare_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));

    public override Task Where_compare_tuple_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_equal(async));

    public override Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_equal(async));

    public override Task Where_compare_tuple_create_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));

    public override Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));

    public override Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_not_equal(async));

    public override Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));
}
