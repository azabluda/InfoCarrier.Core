// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ComplexTypeQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     Querying <em>against</em> complex types — filtering on a member of one, projecting one,
///     comparing two — as opposed to <c>ComplexTypesTrackingTestBase</c>, which tracks them and
///     runs happily on Tier A. A77 established why the two differ: EF's InMemory provider does not
///     translate a complex property access at all, and ships no complex-type query test of any
///     kind. The SQLite one does, so this is the tier the base belongs on.
///     <para>
///         The core base rather than <c>ComplexTypeQueryRelationalTestBase</c>: the relational one
///         asserts SQL, which a client with no database has none of.
///     </para>
/// </remarks>
public class ComplexTypeQuerySqliteInfoCarrierTest(
    ComplexTypeQuerySqliteInfoCarrierTest.ComplexTypeQuerySqliteInfoCarrierFixture fixture)
    : ComplexTypeQueryTestBase<ComplexTypeQuerySqliteInfoCarrierTest.ComplexTypeQuerySqliteInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The six overrides below and the two after them are EF's own, and they are here because
    ///     the *backing store* is relational: `ComplexTypeQueryRelationalTestBase` carries the
    ///     first six (a subquery over a complex type, and a set operation between two different
    ///     ones, are limits every relational provider has) and `ComplexTypeQuerySqliteTest` the
    ///     last two (`ApplyNotSupported`). CLAUDE.md's rule — grep the relational base as well as
    ///     SQLite's own suite — is what finds the first group.
    /// </remarks>
    public override async Task Subquery_over_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SubqueryOverComplexTypesNotSupported("Customer.ShippingAddress#Address"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Subquery_over_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Subquery_over_struct_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SubqueryOverComplexTypesNotSupported("ValuedCustomer.ShippingAddress#AddressStruct"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Subquery_over_struct_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Concat_two_different_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SetOperationOverDifferentStructuralTypes(
                "Customer.ShippingAddress#Address", "Customer.BillingAddress#Address"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Concat_two_different_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Union_two_different_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SetOperationOverDifferentStructuralTypes(
                "Customer.ShippingAddress#Address", "Customer.BillingAddress#Address"),
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Union_two_different_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Concat_two_different_struct_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SetOperationOverDifferentStructuralTypes(
                "ValuedCustomer.ShippingAddress#AddressStruct", "ValuedCustomer.BillingAddress#AddressStruct"),
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Concat_two_different_struct_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Union_two_different_struct_complex_type(bool async)
        => Assert.Equal(
            RelationalStrings.SetOperationOverDifferentStructuralTypes(
                "ValuedCustomer.ShippingAddress#AddressStruct", "ValuedCustomer.BillingAddress#AddressStruct"),
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Union_two_different_struct_complex_type(async))).Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async)))
            .Message);

    /// <inheritdoc cref="Subquery_over_complex_type" />
    public override async Task Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async)))
            .Message);

    public class ComplexTypeQuerySqliteInfoCarrierFixture : ComplexTypeQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ComplexTypeQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
