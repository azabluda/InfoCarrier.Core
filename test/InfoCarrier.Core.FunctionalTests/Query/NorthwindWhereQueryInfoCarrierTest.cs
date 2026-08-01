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
    // InMemory store limitation: structural (anonymous type / tuple) equality is not
    // translated, so the comparison degrades to reference equality and matches nothing.
    // EF Core's own NorthwindWhereQueryInMemoryTest no-ops each of these identically.
    // ---------------------------------------------------------------------------------

    public override Task Where_compare_constructed_equal(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_constructed_multi_value_equal(bool async)
        => Task.CompletedTask;

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
    // InMemory store limitation: ElementAt/ElementAtOrDefault over a custom projection is
    // not supported by the InMemory query pipeline. Also no-opped by EF Core's own class.
    // ---------------------------------------------------------------------------------

    public override Task ElementAt_over_custom_projection_compared_to_not_null(bool async)
        => Task.CompletedTask;

    public override Task ElementAtOrDefault_over_custom_projection_compared_to_null(bool async)
        => Task.CompletedTask;
}
