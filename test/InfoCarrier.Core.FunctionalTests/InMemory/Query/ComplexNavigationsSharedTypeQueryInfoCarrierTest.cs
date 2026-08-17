// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>ComplexNavigationsSharedTypeQueryTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     A33's complex-navigations corpus again, over a model whose levels are <em>shared-type</em>
///     entity types — the same navigation depth, but every entity type is keyed by name and several
///     share one CLR type. That is the case where reading a value by its CLR member is not enough
///     and the model has to be consulted, which is most of what this provider's mapper does.
///     Both overrides are EF's own <c>ComplexNavigationsSharedTypeQueryInMemoryTest</c>.
/// </remarks>
public class ComplexNavigationsSharedTypeQueryInfoCarrierTest(ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsSharedTypeQueryTestBase<ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The same reading as <c>ComplexNavigationsQueryInfoCarrierTest</c>'s: EF issue #23302 is
    ///     a validation error EF's own walk misses for a <c>Join</c> result selector, C73 states
    ///     the refusal on the result element type, and the base's own helper then applies.
    /// </remarks>
    public override Task Join_with_result_selector_returning_queryable_throws_validation_error(bool async)
        => AssertInvalidMaterializationType(
            () => base.Join_with_result_selector_returning_queryable_throws_validation_error(async),
            "IQueryable<Level3>");

    /// <inheritdoc />
    public override Task Correlated_projection_with_first(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    ///     A44 on this model. EF overrides it on `ComplexNavigationsSharedTypeQueryRelationalTestBase`
    ///     for the same reason: client code in a <c>Where</c> decides which rows, so running it
    ///     here means fetching all of them.
    /// </remarks>
    public override Task Complex_query_with_optional_navigations_and_client_side_evaluation(bool async)
        => AssertTranslationFailed(
            () => base.Complex_query_with_optional_navigations_and_client_side_evaluation(async));

    /// <inheritdoc />
    /// <remarks>
    ///     The same rule for an <c>OrderBy</c> key, with the details clause EF's SQLite shared-type
    ///     suite asserts. The declaring type is <c>ComplexNavigationsQueryTestBase</c> — the shared
    ///     -type base derives from it, and `ClientMethodNullableInt` is declared up there.
    /// </remarks>
    public override Task GroupJoin_client_method_in_OrderBy(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.GroupJoin_client_method_in_OrderBy(async),
            CoreStrings.QueryUnableToTranslateMethod(
                "Microsoft.EntityFrameworkCore.Query.ComplexNavigationsQueryTestBase<"
                    + typeof(ComplexNavigationsSharedTypeQueryInfoCarrierFixture).FullName + ">",
                "ClientMethodNullableInt"));
}

/// <summary>
///     <c>ComplexNavigationsCollectionsSharedTypeQueryTestBase</c> on Tier A — the same model,
///     queried through its collections.
/// </summary>
/// <remarks>
///     Every override is EF's own <c>ComplexNavigationsCollectionsSharedTypeQueryInMemoryTest</c>:
///     a non-composed <c>GroupBy</c> is a backing-store limitation, and this backing store is the
///     InMemory one.
/// </remarks>
public class ComplexNavigationsCollectionsSharedTypeQueryInfoCarrierTest(
    ComplexNavigationsSharedTypeQueryInfoCarrierFixture fixture)
    : ComplexNavigationsCollectionsSharedTypeQueryTestBase<ComplexNavigationsSharedTypeQueryInfoCarrierFixture>(fixture)
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
///     The shared-type complex-navigations fixture, wired to an InMemory backend behind the wire.
///     Shared by both classes above, exactly as EF shares its own.
/// </summary>
public class ComplexNavigationsSharedTypeQueryInfoCarrierFixture : ComplexNavigationsSharedTypeQueryFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
}
