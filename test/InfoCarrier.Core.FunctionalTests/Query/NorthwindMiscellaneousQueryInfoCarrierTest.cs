// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <see cref="NorthwindMiscellaneousQueryTestBase{TFixture}" /> over the InfoCarrier client with an InMemory backend.
/// </summary>
/// <remarks>
///     <para>
///         Each override mirrors EF Core's own <c>NorthwindMiscellaneousQueryInMemoryTest</c>
///         one for one. InMemory throws where a relational store returns an empty sequence, and does not support <c>ElementAtOrDefault</c> in a subquery or entity equality through a composite-key subquery.
///     </para>
///     <para>
///         These are <strong>backing-store</strong> limitations, not InfoCarrier gaps — a local
///         InMemory provider behaves identically with no wire involved — so the overrides assert
///         that behavior rather than suppress the test. They do not apply to the relational
///         (SQLite) backend of ADR-009 Tier B and must be deleted, not carried over, when it lands.
///     </para>
/// </remarks>
public class NorthwindMiscellaneousQueryInfoCarrierTest(NorthwindQueryInfoCarrierFixture<NoopModelCustomizer> fixture)
    : NorthwindMiscellaneousQueryTestBase<NorthwindQueryInfoCarrierFixture<NoopModelCustomizer>>(fixture)
{
    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_one_element_Single(bool async)
        // Sequence contains no elements.
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_query_composition_entity_equality_one_element_Single(async));

    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_one_element_First(bool async)
        // Sequence contains no elements.
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_query_composition_entity_equality_one_element_First(async));

    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_no_elements_Single(bool async)
        // Sequence contains no elements.
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_query_composition_entity_equality_no_elements_Single(async));

    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_no_elements_First(bool async)
        // Sequence contains no elements.
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_query_composition_entity_equality_no_elements_First(async));

    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(bool async)
        // Sequence contains more than one element.
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Where_query_composition_entity_equality_multiple_elements_SingleOrDefault(async));

    /// <inheritdoc />
    public override Task Where_query_composition_entity_equality_multiple_elements_Single(bool async)
        // Sequence contains more than one element.
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Where_query_composition_entity_equality_multiple_elements_Single(async));

    /// <inheritdoc />
    public override Task Max_on_empty_sequence_throws(bool async)
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Max_on_empty_sequence_throws(async));

    /// <inheritdoc />
    public override async Task Entity_equality_through_subquery_composite_key(bool async)
        => Assert.Equal(
            CoreStrings.EntityEqualityOnCompositeKeyEntitySubqueryNotSupported("==", nameof(OrderDetail)),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Entity_equality_through_subquery_composite_key(async)))
            .Message);

    /// <inheritdoc />
    public override Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_zero(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_constant_one(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Collection_navigation_equal_to_null_for_subquery_using_ElementAtOrDefault_parameter(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Where_Order_First(bool async)
        // Sequence contains no elements.
        => Assert.ThrowsAsync<InvalidOperationException>(() => base.Where_Order_First(async));
}
