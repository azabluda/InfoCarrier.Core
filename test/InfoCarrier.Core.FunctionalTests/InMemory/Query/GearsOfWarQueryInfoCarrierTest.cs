// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.GearsOfWarModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>GearsOfWarQueryTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     EF's largest single query corpus, and the one that leans hardest on optional navigations,
///     TPH inheritance and null semantics at once. Every override below is EF's own
///     <c>GearsOfWarQueryInMemoryTest</c>, adopted as a set (A31): they are what the InMemory store
///     cannot do, and this backing store is that store. Anything else red here is ours.
/// </remarks>
public class GearsOfWarQueryInfoCarrierTest(GearsOfWarQueryInfoCarrierFixture fixture)
    : GearsOfWarQueryTestBase<GearsOfWarQueryInfoCarrierFixture>(fixture)
{
    /// <remarks>
    ///     Client code in a <c>Where</c> decides *which* rows, so running it here means fetching
    ///     all of them — the line ADR-010 draws and `RejectClientEvaluation` enforces, which is
    ///     also every relational provider's. EF overrides this on
    ///     `GearsOfWarQueryRelationalTestBase` (A27).
    /// </remarks>
    public override Task Client_side_equality_with_parameter_works_with_optional_navigations(bool async)
        => AssertTranslationFailed(
            () => base.Client_side_equality_with_parameter_works_with_optional_navigations(async));

    public override Task Client_member_and_unsupported_string_Equals_in_the_same_query(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Client_member_and_unsupported_string_Equals_in_the_same_query(async),
            CoreStrings.QueryUnableToTranslateMember(nameof(Gear.IsMarcus), nameof(Gear)));

    public override async Task
        Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation_complex(bool async)
        => Assert.Equal(
            "Nullable object must have a value.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base
                .Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation_complex(
                    async))).Message);

    public override async Task Group_by_on_StartsWith_with_null_parameter_as_argument(bool async)
        => Assert.Equal(
            "Value cannot be null. (Parameter 'value')",
            (await Assert.ThrowsAsync<ArgumentNullException>(() => base.Group_by_on_StartsWith_with_null_parameter_as_argument(async)))
            .Message);

    public override async Task Group_by_with_having_StartsWith_with_null_parameter_as_argument(bool async)
        => Assert.Equal(
            "Value cannot be null. (Parameter 'value')",
            (await Assert.ThrowsAsync<ArgumentNullException>(()
                => base.Group_by_with_having_StartsWith_with_null_parameter_as_argument(async))).Message);

    public override async Task OrderBy_StartsWith_with_null_parameter_as_argument(bool async)
        => Assert.Equal(
            "Value cannot be null. (Parameter 'value')",
            (await Assert.ThrowsAsync<ArgumentNullException>(() => base.OrderBy_StartsWith_with_null_parameter_as_argument(async)))
            .Message);

    public override async Task Select_StartsWith_with_null_parameter_as_argument(bool async)
        => Assert.Equal(
            "Value cannot be null. (Parameter 'value')",
            (await Assert.ThrowsAsync<ArgumentNullException>(() => base.Select_StartsWith_with_null_parameter_as_argument(async))).Message);

    // `Projecting_entity_as_well_as_correlated_collection_followed_by_Distinct`, its `complex_`
    // and `of_scalars_` siblings, and `Projecting_some_properties_as_well_as_…` are EF's InMemory
    // overrides for issue #24325 and are deliberately **not** carried here (A39). The `Distinct`
    // in those shapes lands in the residual, so the store never sees the subquery it refuses, and
    // the spec test runs and passes. An override of ours for a limitation this provider does not
    // have is a workaround, and the coverage is the point (ADR-004).
    //
    // `Correlated_collection_with_distinct_3_levels` belongs to that list too, and carrying EF's
    // override for it was the A39 mistake in the one place A39 did not look (C64). It is left
    // **unoverridden and red**, and the red is not a wrong answer: the query runs and this
    // provider's rows agree with the expected ones squad for squad, member for member, weapon
    // count for weapon count. What fails is the base's own assertion, and it fails for a reason
    // that has nothing to do with any provider — the projection is an anonymous type whose
    // `Members` member is a lazily-evaluated `IEnumerable<>`, which the compiler-generated
    // `Equals` compares with `EqualityComparer<T>.Default`, i.e. by reference. Running the base's
    // *expected* query twice over the same in-memory data, with the same `Squad` instance in both
    // results, fails the same assertion. **No correct answer can satisfy it**, which is why every
    // EF provider refuses the query before reaching it: InMemory with
    // `DistinctOnSubqueryNotSupported`, and every relational one with
    // `DistinctOnCollectionNotSupported` on `GearsOfWarQueryRelationalTestBase`.

    public override async Task Projecting_correlated_collection_followed_by_Distinct(bool async)
        // Distinct. Issue #24325.
        => Assert.Equal(
            InMemoryStrings.DistinctOnSubqueryNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Projecting_correlated_collection_followed_by_Distinct(async)))
            .Message);

    public override Task Include_after_SelectMany_throws(bool async)
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Include_after_SelectMany_throws(async));

    public override async Task Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result4(bool async)
        => await Assert.ThrowsAsync<TargetInvocationException>(()
            => base.Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_coalesce_result4(async));

    public override async Task Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_complex_projection_result(bool async)
        => await Assert.ThrowsAsync<TargetInvocationException>(()
            => base.Include_on_GroupJoin_SelectMany_DefaultIfEmpty_with_complex_projection_result(async));

    public override Task Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation(
            bool async)
        // Null protection. Issue #13721.
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Null_semantics_is_correctly_applied_for_function_comparisons_that_take_arguments_from_optional_navigation(async));

    public override Task ElementAt_basic_with_OrderBy(bool async)
        => Task.CompletedTask;

    public override Task ElementAtOrDefault_basic_with_OrderBy(bool async)
        => Task.CompletedTask;

    public override Task ElementAtOrDefault_basic_with_OrderBy_parameter(bool async)
        => Task.CompletedTask;

    public override Task Where_subquery_with_ElementAtOrDefault_equality_to_null_with_composite_key(bool async)
        => Task.CompletedTask;

    public override Task Where_subquery_with_ElementAt_using_column_as_index(bool async)
        => Task.CompletedTask;

    public override Task Where_compare_anonymous_types(bool async)
        => Task.CompletedTask;

    public override Task Subquery_inside_Take_argument(bool async)
        => Task.CompletedTask;

    public override async Task Find_underlying_property_after_GroupJoin_DefaultIfEmpty(bool async)
        => Assert.Equal(
            "Nullable object must have a value.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base
                .Find_underlying_property_after_GroupJoin_DefaultIfEmpty(
                    async))).Message);

    public override Task Join_include_coalesce_simple(bool async)
        => Task.CompletedTask;

    public override Task Join_include_coalesce_nested(bool async)
        => Task.CompletedTask;

    public override Task Join_include_conditional(bool async)
        => Task.CompletedTask;

    // Right join not supported in InMemory
    public override Task Correlated_collections_on_RightJoin_with_predicate(bool async)
        => AssertTranslationFailed(() => base.Correlated_collections_on_RightJoin_with_predicate(async));

    [ConditionalTheory, MemberData(nameof(IsAsyncData))]
    public virtual Task Select_ToString_on_non_nullable_property_of_an_optional_entity(bool async)
        => AssertQuery(
            async,
            ss => ss.Set<CogTag>().Select(x => new { x.Id, SquadIdString = x.Gear.SquadId.ToString() }),
            ss => ss.Set<CogTag>().Select(x => new { x.Id, SquadIdString = x.Gear == null ? null! : x.Gear.SquadId.ToString() }),
            elementSorter: e => e.Id,
            elementAsserter: (e, a) =>
            {
                AssertEqual(e.Id, a.Id);
                AssertEqual(e.SquadIdString, a.SquadIdString);
            });
}

/// <summary>
///     The Gears-of-War fixture, wired to an InMemory backend behind the wire.
/// </summary>
public class GearsOfWarQueryInfoCarrierFixture : GearsOfWarQueryFixtureBase
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
