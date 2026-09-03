// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindGroupByQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A, and the move deletes six overrides.</b> Each Tier A override
///         asserted <c>InMemoryStrings.NonComposedGroupByNotSupported</c> — the InMemory provider
///         cannot translate a <c>GroupBy</c> that is not composed into an aggregate or a
///         projection of its elements. A relational provider translates all of them, and the
///         Tier A class already recorded that the overrides "must be deleted rather than carried
///         over" once a relational backend landed. The relational base adds none of its own.
///     </para>
///     <para>
///         The seven <c>ApplyNotSupported</c> overrides are EF Core's own — <c>APPLY</c> is not
///         SQLite syntax — adopted after measuring rather than copied in advance, and every one is
///         convergence with <c>NorthwindGroupByQuerySqliteTest</c>.
///     </para>
///     <para>
///         <c>Final_GroupBy_nominal_type_entity</c> keeps the override the Tier A class carried,
///         because its reason is store-independent: <c>GroupBy(c =&gt; new RandomClass { … })</c>
///         keys the grouping on a type the server cannot name (ADR-010), so the query never
///         reaches the store. EF's own SQLite class does not override it; this provider must.
///     </para>
/// </remarks>
public class NorthwindGroupByQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindGroupByQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task Select_uncorrelated_collection_with_groupby_works(bool async)
        => AssertApplyNotSupported(() => base.Select_uncorrelated_collection_with_groupby_works(async));

    /// <inheritdoc />
    public override Task Select_uncorrelated_collection_with_groupby_multiple_collections_work(bool async)
        => AssertApplyNotSupported(() => base.Select_uncorrelated_collection_with_groupby_multiple_collections_work(async));

    /// <inheritdoc />
    public override Task Select_uncorrelated_collection_with_groupby_when_outer_is_distinct(bool async)
        => AssertApplyNotSupported(() => base.Select_uncorrelated_collection_with_groupby_when_outer_is_distinct(async));

    /// <inheritdoc />
    public override Task AsEnumerable_in_subquery_for_GroupBy(bool async)
        => AssertApplyNotSupported(() => base.AsEnumerable_in_subquery_for_GroupBy(async));

    /// <inheritdoc />
    public override Task Select_nested_collection_with_groupby(bool async)
        => AssertApplyNotSupported(() => base.Select_nested_collection_with_groupby(async));

    /// <inheritdoc />
    public override Task Complex_query_with_group_by_in_subquery5(bool async)
        => AssertApplyNotSupported(() => base.Complex_query_with_group_by_in_subquery5(async));

    /// <inheritdoc />
    public override Task Select_correlated_collection_after_GroupBy_aggregate_when_identifier_changes_to_complex(bool async)
        => AssertApplyNotSupported(
            () => base.Select_correlated_collection_after_GroupBy_aggregate_when_identifier_changes_to_complex(async));

    /// <summary>
    ///     The one member of the <c>Final_GroupBy</c> family whose key is a client-only type.
    /// </summary>
    /// <remarks>
    ///     <c>GroupBy(c =&gt; new RandomClass { … })</c> keys the grouping on a type the server
    ///     cannot name (ADR-010), so the query is refused before it reaches the store. Unlike its
    ///     Tier A siblings this reason does not depend on the backend, so the override survives the
    ///     move to Tier B.
    /// </remarks>
    public override Task Final_GroupBy_nominal_type_entity(bool async)
        => AssertTranslationFailed(() => base.Final_GroupBy_nominal_type_entity(async));

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);
}
