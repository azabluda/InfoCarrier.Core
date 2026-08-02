// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     The same inherited base as <see cref="NorthwindWhereQueryInfoCarrierTest" />, on ADR-009
///     Tier B (SQLite).
/// </summary>
/// <remarks>
///     <para>
///         Deliberately <strong>no overrides</strong>. The Tier A class carries eight, each
///         asserting a limitation of the backing store or of EF itself; whether they still apply
///         on a backend that genuinely translates is a question, not an assumption, and this
///         class is how it gets answered. Overrides are added here only for what this run
///         actually shows, with the reason stated.
///     </para>
///     <para>
///         The first run of this class is therefore expected to be red, and is information rather
///         than a regression (CLAUDE.md).
///     </para>
/// </remarks>
public class NorthwindWhereQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindWhereQueryTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
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

    // Client evaluation outside the final projection, as on Tier A. EF Core's own
    // NorthwindWhereQueryRelationalTestBase overrides this identically.
    public override Task Where_bool_client_side_negated(bool async)
        => AssertTranslationFailed(() => base.Where_bool_client_side_negated(async));
}
