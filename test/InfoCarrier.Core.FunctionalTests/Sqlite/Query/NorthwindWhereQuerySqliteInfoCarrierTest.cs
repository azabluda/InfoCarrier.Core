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
public class NorthwindWhereQuerySqliteInfoCarrierTest
    : NorthwindWhereQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>
{
    /// <summary>
    ///     Initializes a new instance and forgets the statements of the test before this one.
    /// </summary>
    /// <remarks>
    ///     <b>Per test, because xUnit builds the test class per test</b>, which is how EF's own SQLite
    ///     classes clear their <c>TestSqlLoggerFactory</c>. The recorder belongs to the store, and the
    ///     store to this fixture, so nothing another class runs is in it.
    /// </remarks>
    public NorthwindWhereQuerySqliteInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
        : base(fixture)
        => fixture.TestStore.ServerSqlRecorderOf().Clear();

    /// <summary>
    ///     Asserts the statements the SERVER ran, with EF's own text for this test (#111).
    /// </summary>
    private void AssertSql(params string[] expected)
        => Fixture.TestStore.AssertServerSql(expected);

    /// <inheritdoc />
    /// <remarks>EF's own override, copied: the body is the base test and the statement is EF's.</remarks>
    [UpstreamOverride(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 17, 30)]
    public override async Task Where_ternary_boolean_condition_negated(bool async)
    {
        await base.Where_ternary_boolean_condition_negated(async);

        AssertSql(
            """
SELECT "p"."ProductID", "p"."Discontinued", "p"."ProductName", "p"."SupplierID", "p"."UnitPrice", "p"."UnitsInStock"
FROM "Products" AS "p"
WHERE CASE
    WHEN "p"."UnitsInStock" >= 20 THEN 1
    ELSE 0
END
""");
    }

    /// <inheritdoc cref="Where_ternary_boolean_condition_negated" />
    [UpstreamOverride(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 58, 68)]
    public override async Task Decimal_cast_to_double_works(bool async)
    {
        await base.Decimal_cast_to_double_works(async);

        AssertSql(
            """
SELECT "p"."ProductID", "p"."Discontinued", "p"."ProductName", "p"."SupplierID", "p"."UnitPrice", "p"."UnitsInStock"
FROM "Products" AS "p"
WHERE CAST("p"."UnitPrice" AS REAL) > 100.0
""");
    }

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

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 70, 76,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 78, 84,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 86, 92,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 94, 100,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 102, 108,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 118, 124,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_create_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 126, 132,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 110, 116,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_constructed_multi_value_not_equal(async));

    [StoreIssue(
        IssueTracker.EfCore, 14672,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/NorthwindWhereQuerySqliteTest.cs", 134, 140,
        Justification = "Anonymous type to constant comparison. Issue #14672.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_tuple_create_constructed_multi_value_not_equal(async));
}
