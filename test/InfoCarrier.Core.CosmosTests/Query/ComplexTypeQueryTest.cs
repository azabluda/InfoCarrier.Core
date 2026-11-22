// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using ComplexCustomer = Microsoft.EntityFrameworkCore.TestModels.ComplexTypeModel.Customer;
using CustomerGroup = Microsoft.EntityFrameworkCore.TestModels.ComplexTypeModel.CustomerGroup;
using ValuedCustomer = Microsoft.EntityFrameworkCore.TestModels.ComplexTypeModel.ValuedCustomer;
using ValuedCustomerGroup = Microsoft.EntityFrameworkCore.TestModels.ComplexTypeModel.ValuedCustomerGroup;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

// Pinned official EF Core 11 behavior, without SQL baselines.
#pragma warning disable EF1001 // Assert provider diagnostic resources from the pinned official suite.
public class ComplexTypeQueryTest(ComplexTypeQueryTest.CosmosFixture fixture)
    : ComplexTypeQueryTestBase<ComplexTypeQueryTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ComplexTypeQueryCosmosTest.cs";

    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());

    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 12, 14, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_complex_type_after_subquery(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 16, 19, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_nested_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 21, 24, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_complex_type_on_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 26, 29, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_complex_type_on_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 31, 34, Justification = Upstream.GaveNoReason)]
    public override Task Project_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 36, 39, Justification = Upstream.GaveNoReason)]
    public override Task Project_complex_type_via_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_complex_type_via_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 41, 43, Justification = Upstream.GaveNoReason)]
    public override Task Load_complex_type_after_subquery_on_entity_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Load_complex_type_after_subquery_on_entity_type(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 45, 56, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 58, 69, Justification = Upstream.GaveNoReason)]
    public override Task Select_nested_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_nested_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 71, 82, Justification = Upstream.GaveNoReason)]
    public override Task Select_single_property_on_nested_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_single_property_on_nested_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 84, 96, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type_Where(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type_Where(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 98, 109, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type_Distinct(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type_Distinct(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 111, 123, Justification = Upstream.GaveNoReason)]
    public override Task Complex_type_equals_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Complex_type_equals_complex_type(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 125, 140, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Complex_type_equals_constant(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Complex_type_equals_constant(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 142, 159, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Complex_type_equals_parameter(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Complex_type_equals_parameter(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 161, 163, Justification = Upstream.GaveNoReason)]
    public override Task Subquery_over_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Subquery_over_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 165, 167, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Contains_over_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 169, 170, Justification = Upstream.GaveNoReason)]
    public override Task Concat_complex_type(bool async)
        => AssertTranslationFailedWithDetails(() => base.Concat_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 172, 174, Justification = Upstream.GaveNoReason)]
    public override Task Concat_entity_type_containing_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_entity_type_containing_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 176, 178, Justification = Upstream.GaveNoReason)]
    public override Task Union_entity_type_containing_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_entity_type_containing_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 180, 181, Justification = Upstream.GaveNoReason)]
    public override Task Union_complex_type(bool async)
        => AssertTranslationFailedWithDetails(() => base.Union_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 183, 185, Justification = Upstream.GaveNoReason)]
    public override Task Concat_property_in_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_property_in_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 187, 189, Justification = Upstream.GaveNoReason)]
    public override Task Union_property_in_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_property_in_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 191, 193, Justification = Upstream.GaveNoReason)]
    public override Task Concat_two_different_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_two_different_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 195, 197, Justification = Upstream.GaveNoReason)]
    public override Task Union_two_different_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_two_different_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 199, 211, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Filter_on_property_inside_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 213, 225, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Filter_on_property_inside_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 227, 230, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_struct_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_struct_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 232, 235, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_struct_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_nested_struct_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 237, 240, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 242, 245, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 247, 250, Justification = Upstream.GaveNoReason)]
    public override Task Project_struct_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_struct_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 252, 255, Justification = Upstream.GaveNoReason)]
    public override Task Project_nullable_struct_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_nullable_struct_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 257, 260, Justification = Upstream.GaveNoReason)]
    public override Task Project_struct_complex_type_via_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_struct_complex_type_via_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 262, 264, Justification = Upstream.GaveNoReason)]
    public override Task Load_struct_complex_type_after_subquery_on_entity_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Load_struct_complex_type_after_subquery_on_entity_type(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 266, 277, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 279, 290, Justification = Upstream.GaveNoReason)]
    public override Task Select_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 292, 303, Justification = Upstream.GaveNoReason)]
    public override Task Select_single_property_on_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_single_property_on_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 305, 317, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type_Where(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type_Where(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 319, 330, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type_Distinct(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type_Distinct(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 332, 344, Justification = Upstream.GaveNoReason)]
    public override Task Struct_complex_type_equals_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Struct_complex_type_equals_struct_complex_type(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 346, 361, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Struct_complex_type_equals_constant(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Struct_complex_type_equals_constant(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 363, 380, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Struct_complex_type_equals_parameter(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Struct_complex_type_equals_parameter(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 382, 384, Justification = Upstream.GaveNoReason)]
    public override Task Subquery_over_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Subquery_over_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 386, 388, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Contains_over_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 390, 392, Justification = Upstream.GaveNoReason)]
    public override Task Concat_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 394, 396, Justification = Upstream.GaveNoReason)]
    public override Task Concat_entity_type_containing_struct_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_entity_type_containing_struct_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 398, 400, Justification = Upstream.GaveNoReason)]
    public override Task Union_entity_type_containing_struct_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_entity_type_containing_struct_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 402, 404, Justification = Upstream.GaveNoReason)]
    public override Task Union_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 406, 408, Justification = Upstream.GaveNoReason)]
    public override Task Concat_property_in_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_property_in_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 410, 412, Justification = Upstream.GaveNoReason)]
    public override Task Union_property_in_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_property_in_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 414, 416, Justification = Upstream.GaveNoReason)]
    public override Task Concat_two_different_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_two_different_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 418, 420, Justification = Upstream.GaveNoReason)]
    public override Task Union_two_different_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_two_different_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 422, 425, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 427, 429, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_nested_complex_type_twice_with_pushdown(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 431, 434, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 436, 439, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 441, 444, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 446, 449, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_struct_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_struct_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 451, 454, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 456, 459, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_struct_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 461, 464, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 466, 469, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 471, 474, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_nested_complex_type_projected_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 476, 479, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 481, 483, Justification = Upstream.GaveNoReason)]
    public override Task Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => AssertTranslationFailed(()
            => base.Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 485, 486, Justification = Upstream.GaveNoReason)]
    public override Task Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => AssertTranslationFailed(() => base.Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async));

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 488, 499, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]

    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_property_in_nested_complex_type(bool async)
    {
        await base.GroupBy_over_property_in_nested_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 501, 510, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_complex_type(bool async)
    {
        await base.GroupBy_over_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 512, 521, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_nested_complex_type(bool async)
    {
        await base.GroupBy_over_nested_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 523, 532, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Entity_with_complex_type_with_group_by_and_first(bool async)
    {
        await base.Entity_with_complex_type_with_group_by_and_first(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 534, 539, Justification = Upstream.GaveNoReason)]

    public override Task Projecting_property_of_complex_type_using_left_join_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Projecting_property_of_complex_type_using_left_join_with_pushdown(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 541, 544, Justification = Upstream.GaveNoReason)]
    public override Task Projecting_complex_from_optional_navigation_using_conditional(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Projecting_complex_from_optional_navigation_using_conditional(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 546, 549, Justification = Upstream.GaveNoReason)]
    public override Task Project_entity_with_complex_type_pushdown_and_then_left_join(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_entity_with_complex_type_pushdown_and_then_left_join(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 551, 569, Skip = true, Justification = "Complex-type equality comparisons return no results")]

    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override async Task Complex_type_equals_parameter_with_nested_types_with_property_of_same_name()
    {
        SkipOnLinuxEmulator();

        await base.Complex_type_equals_parameter_with_nested_types_with_property_of_same_name();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 571, 580, Justification = Upstream.GaveNoReason)]
    public override async Task Projecting_complex_property_does_not_auto_include_owned_types()
    {
        await base.Projecting_complex_property_does_not_auto_include_owned_types();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 582, 598, Justification = Upstream.GaveNoReason)]
    public override async Task Optional_complex_type_with_discriminator()
    {
        await base.Optional_complex_type_with_discriminator();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 600, 610, Justification = Upstream.GaveNoReason)]
    public override async Task Non_optional_complex_type_with_all_nullable_properties()
    {
        await base.Non_optional_complex_type_with_all_nullable_properties();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 612, 620, Justification = Upstream.GaveNoReason)]
    public override async Task Non_optional_complex_type_with_all_nullable_properties_via_left_join()
    {
        Assert.Equal(
            CosmosStrings.UpdateConflict("1"),
            (await Assert.ThrowsAsync<DbUpdateException>(
                base.Non_optional_complex_type_with_all_nullable_properties_via_left_join)).Message);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 622, 631, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_and_shadow_property()
    {
        await base.Nullable_complex_type_with_discriminator_and_shadow_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 633, 649, Justification = Upstream.GaveNoReason)]
    public override async Task Update_entity_with_nullable_complex_type_and_discriminator_does_not_throw()
    {
        await base.Update_entity_with_nullable_complex_type_and_discriminator_does_not_throw();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 651, 667, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_null_to_non_null_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_null_to_non_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 669, 685, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_non_null_to_null_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_non_null_to_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 687, 703, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_update_non_null_entity_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_update_non_null_entity_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 705, 706, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_set_to_different_value()
        => await base.Nullable_complex_type_with_discriminator_set_to_different_value();

    [StoreLimit(UpstreamRepository.EfCore, Source, 708, 737, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_set_to_null()
    {
        // On Cosmos, setting the discriminator shadow property to null doesn't affect materialization
        // because the complex property's data is still present in the JSON document.
        var contextFactory = await InitializeNonSharedTest<Context38119>();

        Guid entityId;
        await using (var context = contextFactory.CreateDbContext())
        {
            var entity = new Context38119.EntityType
            {
                Id = Guid.NewGuid(),
                Prop = new Context38119.OptionalComplexProperty { OptionalValue = true }
            };
            context.Add(entity);
            entityId = entity.Id;

            var discriminatorEntry = context.Entry(entity).ComplexProperty(e => e.Prop).Property("Discriminator");
            Assert.Equal("OptionalComplexProperty", discriminatorEntry.CurrentValue);
            discriminatorEntry.CurrentValue = null;
            await context.SaveChangesAsync();
        }

        await using (var context = contextFactory.CreateDbContext())
        {
            var entity = await context.Set<Context38119.EntityType>().SingleAsync(e => e.Id == entityId);
            Assert.NotNull(entity.Prop);
            Assert.True(entity.Prop.OptionalValue);
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 739, 755, Justification = Upstream.GaveNoReason)]
    public override async Task Nested_nullable_complex_type_with_discriminator_null_to_non_null_roundtrip()
    {
        await base.Nested_nullable_complex_type_with_discriminator_null_to_non_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 757, 768, Justification = Upstream.GaveNoReason)]
    public override async Task Can_query_by_complex_type_property_with_index()
    {
        await base.Can_query_by_complex_type_property_with_index();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 770, 786, Justification = Upstream.GaveNoReason)]
    public override async Task Can_update_entity_with_index_on_complex_type_property()
    {
        await base.Can_update_entity_with_index_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 788, 803, Justification = Upstream.GaveNoReason)]
    public override async Task Can_delete_entity_with_index_on_complex_type_property()
    {
        await base.Can_delete_entity_with_index_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 805, 816, Justification = Upstream.GaveNoReason)]
    public override async Task Can_query_by_alternate_key_on_complex_type_property()
    {
        await base.Can_query_by_alternate_key_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 818, 855, Justification = Upstream.GaveNoReason)]
    public override async Task Can_save_batch_swapping_alternate_key_values_on_complex_type_property()
    {
        // Unlike relational providers, Cosmos can't ORDER BY the primary-key path 'Id.Id': the model defines an
        // explicit index (on Address.PostalCode), which disables automatic indexing and therefore excludes the
        // 'Id.Id' path from the index. Order client-side instead - the test's intent (reading the alternate-key
        // values while building batch edges) is unaffected.
        var contextFactory = await InitializeNonSharedTest<Context31246>(
            seed: context =>
            {
                context.AddRange(
                    new Context31246.Person
                    {
                        Id = new Context31246.StronglyTypedId(1),
                        Address = new Context31246.Address { City = "Seattle", PostalCode = "98101" }
                    },
                    new Context31246.Person
                    {
                        Id = new Context31246.StronglyTypedId(2),
                        Address = new Context31246.Address { City = "Redmond", PostalCode = "98052" }
                    });
                return context.SaveChangesAsync();
            });

        await using (var context = contextFactory.CreateDbContext())
        {
            var people = (await context.Set<Context31246.Person>().ToListAsync()).OrderBy(p => p.Id.Id).ToList();
            people[0].Address.PostalCode = "98103";
            people[1].Address.PostalCode = "98054";
            await context.SaveChangesAsync();
        }

        await using (var context = contextFactory.CreateDbContext())
        {
            var people = (await context.Set<Context31246.Person>().ToListAsync()).OrderBy(p => p.Id.Id).ToList();
            Assert.Equal("98103", people[0].Address.PostalCode);
            Assert.Equal("98054", people[1].Address.PostalCode);
        }
    }

    [Fact]
    public virtual async Task Can_use_complex_type_key_with_discriminator_in_json_id()
    {
        var contextFactory = await InitializeNonSharedTest<ComplexKeyDiscriminatorContext>(
            seed: context =>
            {
                context.AddRange(
                    new ComplexKeyDiscriminatorContext.Customer { Id = new ComplexKeyDiscriminatorContext.CustomerId(1), Name = "Alice" },
                    new ComplexKeyDiscriminatorContext.Customer { Id = new ComplexKeyDiscriminatorContext.CustomerId(2), Name = "Bob" });
                return context.SaveChangesAsync();
            });

        await using var context = contextFactory.CreateDbContext();

        var customer = await context.Set<ComplexKeyDiscriminatorContext.Customer>().SingleAsync(c => c.Id.Value == 1, TestContext.Current.CancellationToken);
        Assert.Equal(new ComplexKeyDiscriminatorContext.CustomerId(1), customer.Id);
        Assert.Equal("Alice", customer.Name);


    }

    [Fact]
    public virtual async Task Can_use_nested_complex_type_key_with_discriminator_in_json_id()
    {
        var contextFactory = await InitializeNonSharedTest<NestedComplexKeyDiscriminatorContext>(
            seed: context =>
            {
                context.AddRange(
                    new NestedComplexKeyDiscriminatorContext.Order
                    {
                        Key = new NestedComplexKeyDiscriminatorContext.OrderKey
                        {
                            Inner = new NestedComplexKeyDiscriminatorContext.InnerKey { Value = 1 }
                        },
                        Description = "First"
                    },
                    new NestedComplexKeyDiscriminatorContext.Order
                    {
                        Key = new NestedComplexKeyDiscriminatorContext.OrderKey
                        {
                            Inner = new NestedComplexKeyDiscriminatorContext.InnerKey { Value = 2 }
                        },
                        Description = "Second"
                    });
                return context.SaveChangesAsync();
            });

        await using var context = contextFactory.CreateDbContext();

        var order = await context.Set<NestedComplexKeyDiscriminatorContext.Order>().SingleAsync(o => o.Key.Inner.Value == 2, TestContext.Current.CancellationToken);
        Assert.Equal(2, order.Key.Inner.Value);
        Assert.Equal("Second", order.Description);


    }

    protected class ComplexKeyDiscriminatorContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>(b =>
            {
                b.ComplexProperty(e => e.Id);
                b.HasKey(e => e.Id.Value);
                b.Property(e => e.Id.Value).ValueGeneratedNever();
                b.HasDiscriminatorInJsonId();
            });

        public readonly record struct CustomerId(int Value);

        public class Customer
        {
            public CustomerId Id { get; set; }
            public string Name { get; set; } = null!;
        }
    }

    protected class NestedComplexKeyDiscriminatorContext(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(b =>
            {
                b.ComplexProperty(e => e.Key, kb => kb.ComplexProperty(k => k.Inner));
                b.HasKey(e => e.Key.Inner.Value);
                b.Property(e => e.Key.Inner.Value).ValueGeneratedNever();
                b.HasDiscriminatorInJsonId();
            });

        public class Order
        {
            public OrderKey Key { get; set; } = null!;
            public string Description { get; set; } = null!;
        }

        public class OrderKey
        {
            public InnerKey Inner { get; set; } = null!;
        }

        public class InnerKey
        {
            public int Value { get; set; }
        }
    }

    private static void SkipOnLinuxEmulator()
        => throw SkipException.ForSkip("Complex-type equality comparisons return no results");

    private static async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async) await test(true);
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }

    public class CosmosFixture : ComplexTypeQueryFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(
                CosmosEventId.NoPartitionKeyDefined, CoreEventId.MappedEntityTypeIgnoredWarning));
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.Entity<ComplexCustomer>().ToContainer("Customers");
            modelBuilder.Entity<CustomerGroup>().ToContainer("CustomerGroups");
            modelBuilder.Entity<ValuedCustomer>().ToContainer("ValuedCustomers");
            modelBuilder.Entity<ValuedCustomerGroup>().ToContainer("ValuedCustomerGroups");
        }
    }
}
