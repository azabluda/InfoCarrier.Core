// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NorthwindFunctionsQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A.</b> The Tier A class was a bare adoption of the core base with no
///         overrides; the relational base adds none of its own beyond the relational query
///         asserter. A base belongs to exactly one tier (CLAUDE.md), and every function this base
///         exercises translates on a relational backend.
///     </para>
///     <para>
///         The four overrides below are EF Core's own — <c>NorthwindFunctionsQuerySqliteTest</c>
///         overrides exactly these with <c>AssertTranslationFailed</c>, and the four measured red
///         here for the same reason: <c>Math.Round</c> / <c>Math.Truncate</c> inside a
///         <c>Sum</c> projection has no SQLite translation. A query that reaches the store and is
///         refused by it is convergence with the reference provider, not a defect here.
///     </para>
/// </remarks>
public class NorthwindFunctionsQueryInfoCarrierTest(NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer> fixture)
    : NorthwindFunctionsQueryRelationalTestBase<NorthwindQueryInfoCarrierSqliteFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task Sum_over_round_works_correctly_in_projection(bool async)
        => AssertTranslationFailed(() => base.Sum_over_round_works_correctly_in_projection(async));

    /// <inheritdoc />
    public override Task Sum_over_round_works_correctly_in_projection_2(bool async)
        => AssertTranslationFailed(() => base.Sum_over_round_works_correctly_in_projection_2(async));

    /// <inheritdoc />
    public override Task Sum_over_truncate_works_correctly_in_projection(bool async)
        => AssertTranslationFailed(() => base.Sum_over_truncate_works_correctly_in_projection(async));

    /// <inheritdoc />
    public override Task Sum_over_truncate_works_correctly_in_projection_2(bool async)
        => AssertTranslationFailed(() => base.Sum_over_truncate_works_correctly_in_projection_2(async));
}
