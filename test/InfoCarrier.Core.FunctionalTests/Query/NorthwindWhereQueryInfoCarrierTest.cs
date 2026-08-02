// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     The first Northwind spec-test class for InfoCarrier (F7): inherits the
///     <see cref="NorthwindWhereQueryTestBase{TFixture}" /> suite via the InfoCarrier fixture.
/// </summary>
/// <remarks>
///     <para>
///         Overrides here fall into exactly one category: <strong>tests the backing store
///         cannot support</strong>. This fixture runs on the InMemory backend (ADR-009 Tier A),
///         and every override below is also no-opped by EF Core's own
///         <c>NorthwindWhereQueryInMemoryTest</c> — the limitation is InMemory's, not
///         InfoCarrier's, and it would be equally present for a local InMemory provider.
///     </para>
///     <para>
///         Tests that fail because InfoCarrier has <em>not yet implemented</em> something are
///         deliberately <strong>not</strong> overridden — they stay red and are tracked in
///         <c>docs/implementation-plan.md</c>. Do not add an override to make the suite green
///         (CLAUDE.md).
///     </para>
///     <para>
///         When the SQLite backend lands (ADR-009 Tier B, roadmap M3) these store limitations
///         no longer apply, so this class must be split: the InMemory-backed class keeps the
///         overrides, the SQLite-backed class inherits the base unmodified.
///     </para>
/// </remarks>
public class NorthwindWhereQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindWhereQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    // ---------------------------------------------------------------------------------
    // Category 3 — UPSTREAM EF CORE LIMITATION, not a store limitation.
    //
    // Anonymous-type / tuple structural equality against a constant is not translated by EF
    // Core on any provider: EF Core issue #14672. EF's own NorthwindWhereQuerySqliteTest
    // overrides these same eight with AssertTranslationFailed, so moving to the SQLite backend
    // (ADR-009 Tier B) will NOT fix them — the override changes shape rather than disappearing,
    // because relational throws a translation failure where InMemory silently matches nothing.
    //
    // (Originally recorded as an InMemory store limitation in G4c. Corrected after checking
    // EF's SQLite class.)
    //
    // The three *anonymous-type* cases are now real assertions rather than no-ops: this
    // provider refuses them, because an anonymous type overrides Equals structurally but not ==,
    // so evaluating one on the client compares two freshly allocated objects by reference —
    // always false, no error, a plausible wrong answer.
    //
    // The six Tuple cases stay no-ops. Tuple<> is a type the server knows, so the comparison is
    // shipped rather than refused, and InMemory then client-evaluates it to the same silent
    // false. On Tier B the server reports the translation failure instead, which is why the
    // SQLite class asserts where this one no-ops — the same divergence already noted above.
    // ---------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------
    // Category 4 — CLIENT EVALUATION OUTSIDE THE FINAL PROJECTION.
    //
    // The base expects success because the InMemory provider client-evaluates everything
    // in-process. A remoting provider cannot: running this predicate on the client means
    // fetching the whole table first, silently. EF Core's own
    // NorthwindWhereQueryRelationalTestBase overrides this test with AssertTranslationFailed
    // for exactly that reason, and Tier B (SQLite) will keep it -- this override does not
    // disappear when the backend changes, unlike the store limitations above.
    // ---------------------------------------------------------------------------------

    public override Task Where_bool_client_side_negated(bool async)
        => AssertTranslationFailed(() => base.Where_bool_client_side_negated(async));

    public override Task Where_compare_constructed_multi_value_not_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_not_equal(async));

    public override Task Where_compare_constructed_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_equal(async));

    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => AssertTranslationFailed(() => base.Where_compare_constructed_multi_value_equal(async));

    public override Task Where_compare_tuple_constructed_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_tuple_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_tuple_create_constructed_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_tuple_create_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_tuple_constructed_multi_value_not_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_tuple_create_constructed_multi_value_not_equal(bool async)
        => Task.CompletedTask;

    // ---------------------------------------------------------------------------------
    // Category 2 — genuine InMemory store limitation. ElementAt/ElementAtOrDefault over a
    // custom projection is unsupported by the InMemory query pipeline. EF's SQLite class does
    // NOT override these, so they should pass once the SQLite backend lands (ADR-009 Tier B)
    // and these two overrides can then be deleted.
    // ---------------------------------------------------------------------------------

    public override Task ElementAt_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    public override Task ElementAtOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;
}
