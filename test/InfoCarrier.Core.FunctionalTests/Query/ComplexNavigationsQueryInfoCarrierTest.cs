// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>ComplexNavigationsQueryTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     The deepest navigation corpus EF has: optional and required one-to-one chains four levels
///     down, each level a separate entity type. Where <c>InheritanceRelationships</c> tests which
///     type a navigation names, this tests how far a query can walk before the split has to decide
///     what travels. Both overrides are EF's own <c>ComplexNavigationsQueryInMemoryTest</c>.
/// </remarks>
public class ComplexNavigationsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsQueryTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Join_with_result_selector_returning_queryable_throws_validation_error(bool async)
        // Expression cannot be used for return type. EF issue #23302, and EF's InMemory test says so.
        => Assert.ThrowsAsync<ArgumentException>(
            () => base.Join_with_result_selector_returning_queryable_throws_validation_error(async));

    /// <inheritdoc />
    public override Task Correlated_projection_with_first(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    ///     Client code in a <c>Where</c> decides *which* rows, so running it here means fetching
    ///     all of them — which is the line ADR-010 draws and `RejectClientEvaluation` enforces,
    ///     the same line every relational provider draws. EF overrides this on
    ///     `ComplexNavigationsQueryRelationalTestBase`, not in any one provider's suite (A27).
    /// </remarks>
    public override Task Complex_query_with_optional_navigations_and_client_side_evaluation(bool async)
        => AssertTranslationFailed(
            () => base.Complex_query_with_optional_navigations_and_client_side_evaluation(async));

    /// <inheritdoc />
    /// <remarks>
    ///     The same rule for an <c>OrderBy</c> key. EF's SQLite suite asserts the details clause
    ///     too, naming the client method; adopted with it.
    /// </remarks>
    public override Task GroupJoin_client_method_in_OrderBy(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.GroupJoin_client_method_in_OrderBy(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.ComplexNavigationsQueryTestBase<"
                    + typeof(ComplexNavigationsQueryInfoCarrierFixture).FullName + ">",
                "ClientMethodNullableInt"));
}

/// <summary>
///     <c>ComplexNavigationsCollectionsQueryTestBase</c> on Tier A — the same model, queried
///     through its collections.
/// </summary>
/// <remarks>
///     Every override is EF's own <c>ComplexNavigationsCollectionsQueryInMemoryTest</c>: a
///     non-composed <c>GroupBy</c> is a backing-store limitation, and this backing store is the
///     InMemory one.
/// </remarks>
public class ComplexNavigationsCollectionsQueryInfoCarrierTest(ComplexNavigationsQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsQueryTestBase<ComplexNavigationsQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection_nested(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection_nested(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection_reference(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection_multiple(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection_multiple(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_collection_reference_same_level(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_collection_reference_same_level(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_reference(async),
            InMemoryStrings.NonComposedGroupByNotSupported);

    /// <inheritdoc />
    public override Task Final_GroupBy_property_entity_Include_reference_multiple(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Final_GroupBy_property_entity_Include_reference_multiple(async),
            InMemoryStrings.NonComposedGroupByNotSupported);
}

/// <summary>
///     The complex-navigations fixture, wired to an InMemory backend behind the wire. Shared by
///     both classes above, exactly as EF shares its own.
/// </summary>
public class ComplexNavigationsQueryInfoCarrierFixture : ComplexNavigationsQueryFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context));
}
