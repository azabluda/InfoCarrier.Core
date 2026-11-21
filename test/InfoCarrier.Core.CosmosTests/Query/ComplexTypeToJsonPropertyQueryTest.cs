// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
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
public class ComplexTypeToJsonPropertyQueryTest(ComplexTypeToJsonPropertyQueryTest.CosmosFixture fixture)
    : ComplexTypeQueryTestBase<ComplexTypeToJsonPropertyQueryTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ComplexTypeToJsonPropertyQueryCosmosTest.cs";

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

    [StoreLimit(UpstreamRepository.EfCore, Source, 14, 16, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_complex_type_after_subquery(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 18, 21, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_nested_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 23, 26, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_complex_type_on_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 31, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_complex_type_on_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_complex_type_on_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 33, 36, Justification = Upstream.GaveNoReason)]
    public override Task Project_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 38, 41, Justification = Upstream.GaveNoReason)]
    public override Task Project_complex_type_via_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_complex_type_via_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 43, 45, Justification = Upstream.GaveNoReason)]
    public override Task Load_complex_type_after_subquery_on_entity_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Load_complex_type_after_subquery_on_entity_type(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 47, 58, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 60, 71, Justification = Upstream.GaveNoReason)]
    public override Task Select_nested_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_nested_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 73, 84, Justification = Upstream.GaveNoReason)]
    public override Task Select_single_property_on_nested_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_single_property_on_nested_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 86, 98, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type_Where(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type_Where(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 100, 111, Justification = Upstream.GaveNoReason)]
    public override Task Select_complex_type_Distinct(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_complex_type_Distinct(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 113, 125, Justification = Upstream.GaveNoReason)]
    public override Task Complex_type_equals_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Complex_type_equals_complex_type(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 127, 142, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Complex_type_equals_constant(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Complex_type_equals_constant(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 144, 161, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Complex_type_equals_parameter(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Complex_type_equals_parameter(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 163, 165, Justification = Upstream.GaveNoReason)]
    public override Task Subquery_over_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Subquery_over_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 167, 169, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Contains_over_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 171, 172, Justification = Upstream.GaveNoReason)]
    public override Task Concat_complex_type(bool async)
        => AssertTranslationFailedWithDetails(() => base.Concat_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 174, 176, Justification = Upstream.GaveNoReason)]
    public override Task Concat_entity_type_containing_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_entity_type_containing_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 178, 180, Justification = Upstream.GaveNoReason)]
    public override Task Union_entity_type_containing_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_entity_type_containing_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 182, 183, Justification = Upstream.GaveNoReason)]
    public override Task Union_complex_type(bool async)
        => AssertTranslationFailedWithDetails(() => base.Union_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 185, 187, Justification = Upstream.GaveNoReason)]
    public override Task Concat_property_in_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_property_in_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 189, 191, Justification = Upstream.GaveNoReason)]
    public override Task Union_property_in_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_property_in_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 193, 195, Justification = Upstream.GaveNoReason)]
    public override Task Concat_two_different_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_two_different_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 197, 199, Justification = Upstream.GaveNoReason)]
    public override Task Union_two_different_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_two_different_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 201, 213, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Filter_on_property_inside_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 215, 227, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Filter_on_property_inside_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 229, 232, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_struct_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_struct_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 234, 237, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_property_inside_nested_struct_complex_type_after_subquery(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_property_inside_nested_struct_complex_type_after_subquery(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 239, 242, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_struct_complex_type_on_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 244, 247, Justification = Upstream.GaveNoReason)]
    public override Task Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Filter_on_required_property_inside_required_struct_complex_type_on_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 249, 252, Justification = Upstream.GaveNoReason)]
    public override Task Project_struct_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_struct_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 254, 257, Justification = Upstream.GaveNoReason)]
    public override Task Project_nullable_struct_complex_type_via_optional_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_nullable_struct_complex_type_via_optional_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 259, 262, Justification = Upstream.GaveNoReason)]
    public override Task Project_struct_complex_type_via_required_navigation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_struct_complex_type_via_required_navigation(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(ValuedCustomer), nameof(ValuedCustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 264, 266, Justification = Upstream.GaveNoReason)]
    public override Task Load_struct_complex_type_after_subquery_on_entity_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Load_struct_complex_type_after_subquery_on_entity_type(async), CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 268, 279, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 281, 292, Justification = Upstream.GaveNoReason)]
    public override Task Select_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 294, 305, Justification = Upstream.GaveNoReason)]
    public override Task Select_single_property_on_nested_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_single_property_on_nested_struct_complex_type(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 307, 319, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type_Where(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type_Where(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 321, 332, Justification = Upstream.GaveNoReason)]
    public override Task Select_struct_complex_type_Distinct(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Select_struct_complex_type_Distinct(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 334, 346, Justification = Upstream.GaveNoReason)]
    public override Task Struct_complex_type_equals_struct_complex_type(bool async)
        => NoSyncTest(
            async, async async =>
            {
                await base.Struct_complex_type_equals_struct_complex_type(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 348, 363, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Struct_complex_type_equals_constant(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Struct_complex_type_equals_constant(async);


            });

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 365, 382, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override Task Struct_complex_type_equals_parameter(bool async)
        => NoSyncTest(
            async, async async =>
            {
                SkipOnLinuxEmulator();

                await base.Struct_complex_type_equals_parameter(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 384, 386, Justification = Upstream.GaveNoReason)]
    public override Task Subquery_over_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Subquery_over_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 388, 390, Justification = Upstream.GaveNoReason)]
    public override Task Contains_over_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Contains_over_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 392, 394, Justification = Upstream.GaveNoReason)]
    public override Task Concat_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 396, 398, Justification = Upstream.GaveNoReason)]
    public override Task Concat_entity_type_containing_struct_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_entity_type_containing_struct_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 400, 402, Justification = Upstream.GaveNoReason)]
    public override Task Union_entity_type_containing_struct_complex_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_entity_type_containing_struct_complex_property(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 404, 406, Justification = Upstream.GaveNoReason)]
    public override Task Union_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 408, 410, Justification = Upstream.GaveNoReason)]
    public override Task Concat_property_in_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_property_in_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 412, 414, Justification = Upstream.GaveNoReason)]
    public override Task Union_property_in_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_property_in_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 416, 418, Justification = Upstream.GaveNoReason)]
    public override Task Concat_two_different_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Concat_two_different_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 420, 422, Justification = Upstream.GaveNoReason)]
    public override Task Union_two_different_struct_complex_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_two_different_struct_complex_type(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 424, 427, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 429, 431, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_nested_complex_type_twice_with_pushdown(async), CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 433, 436, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 438, 441, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 443, 446, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_struct_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 448, 451, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_struct_nested_complex_type_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_struct_nested_complex_type_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 453, 456, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_entity_with_struct_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 458, 461, Justification = Upstream.GaveNoReason)]
    public override Task Project_same_struct_nested_complex_type_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_same_struct_nested_complex_type_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 463, 466, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 468, 471, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_entity_with_nested_complex_type_projected_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 473, 476, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_nested_complex_type_projected_twice_with_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 478, 481, Justification = Upstream.GaveNoReason)]
    public override Task Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Union_of_same_nested_complex_type_projected_twice_with_double_pushdown(async),
            CosmosStrings.NonCorrelatedSubqueriesNotSupported);

    [StoreLimit(UpstreamRepository.EfCore, Source, 483, 485, Justification = Upstream.GaveNoReason)]
    public override Task Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => AssertTranslationFailed(()
            => base.Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 487, 488, Justification = Upstream.GaveNoReason)]
    public override Task Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => AssertTranslationFailed(() => base.Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async));

    [StoreDefect("2.2", UpstreamRepository.EfCore, Source, 490, 496, Skip = true, Justification = "Complex-type equality comparisons return no results")]
    // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/288 (Complex-type equality comparisons return no results)
    public override async Task Complex_type_equals_parameter_with_nested_types_with_property_of_same_name()
    {
        SkipOnLinuxEmulator();

        await base.Complex_type_equals_parameter_with_nested_types_with_property_of_same_name();
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 498, 507, Justification = Upstream.GaveNoReason)]
    public override async Task Projecting_complex_property_does_not_auto_include_owned_types()
    {
        await base.Projecting_complex_property_does_not_auto_include_owned_types();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 509, 525, Justification = Upstream.GaveNoReason)]
    public override async Task Optional_complex_type_with_discriminator()
    {
        await base.Optional_complex_type_with_discriminator();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 527, 537, Justification = Upstream.GaveNoReason)]
    public override async Task Non_optional_complex_type_with_all_nullable_properties()
    {
        await base.Non_optional_complex_type_with_all_nullable_properties();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 539, 547, Justification = Upstream.GaveNoReason)]
    public override async Task Non_optional_complex_type_with_all_nullable_properties_via_left_join()
    {
        Assert.Equal(
            CosmosStrings.UpdateConflict("1"),
            (await Assert.ThrowsAsync<DbUpdateException>(
                base.Non_optional_complex_type_with_all_nullable_properties_via_left_join)).Message);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 549, 558, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_and_shadow_property()
    {
        await base.Nullable_complex_type_with_discriminator_and_shadow_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 560, 576, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_null_to_non_null_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_null_to_non_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 578, 594, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_non_null_to_null_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_non_null_to_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 596, 612, Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_complex_type_with_discriminator_update_non_null_entity_roundtrip()
    {
        await base.Nullable_complex_type_with_discriminator_update_non_null_entity_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 614, 615, Justification = Upstream.GaveNoReason)]
    public override Task Nullable_complex_type_with_discriminator_set_to_different_value()
        => base.Nullable_complex_type_with_discriminator_set_to_different_value();

    [StoreLimit(UpstreamRepository.EfCore, Source, 617, 646, Justification = Upstream.GaveNoReason)]
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

    [StoreLimit(UpstreamRepository.EfCore, Source, 648, 664, Justification = Upstream.GaveNoReason)]
    public override async Task Nested_nullable_complex_type_with_discriminator_null_to_non_null_roundtrip()
    {
        await base.Nested_nullable_complex_type_with_discriminator_null_to_non_null_roundtrip();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 666, 682, Justification = Upstream.GaveNoReason)]
    public override async Task Update_entity_with_nullable_complex_type_and_discriminator_does_not_throw()
    {
        await base.Update_entity_with_nullable_complex_type_and_discriminator_does_not_throw();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 684, 695, Justification = Upstream.GaveNoReason)]
    public override async Task Can_query_by_complex_type_property_with_index()
    {
        await base.Can_query_by_complex_type_property_with_index();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 697, 713, Justification = Upstream.GaveNoReason)]
    public override async Task Can_update_entity_with_index_on_complex_type_property()
    {
        await base.Can_update_entity_with_index_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 715, 730, Justification = Upstream.GaveNoReason)]
    public override async Task Can_delete_entity_with_index_on_complex_type_property()
    {
        await base.Can_delete_entity_with_index_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 732, 743, Justification = Upstream.GaveNoReason)]
    public override async Task Can_query_by_alternate_key_on_complex_type_property()
    {
        await base.Can_query_by_alternate_key_on_complex_type_property();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 745, 782, Justification = Upstream.GaveNoReason)]
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

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 784, 795, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]

    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_property_in_nested_complex_type(bool async)
    {
        await base.GroupBy_over_property_in_nested_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 797, 806, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_complex_type(bool async)
    {
        await base.GroupBy_over_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 808, 817, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task GroupBy_over_nested_complex_type(bool async)
    {
        await base.GroupBy_over_nested_complex_type(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 819, 828, Skip = true, Justification = "#17313 Cosmos: Translate GroupBy")]
    [Theory(Skip = "#17313 Cosmos: Translate GroupBy")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Entity_with_complex_type_with_group_by_and_first(bool async)
    {
        await base.Entity_with_complex_type_with_group_by_and_first(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 830, 835, Justification = Upstream.GaveNoReason)]

    public override Task Projecting_property_of_complex_type_using_left_join_with_pushdown(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Projecting_property_of_complex_type_using_left_join_with_pushdown(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 837, 840, Justification = Upstream.GaveNoReason)]
    public override Task Projecting_complex_from_optional_navigation_using_conditional(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Projecting_complex_from_optional_navigation_using_conditional(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery("Customer", nameof(CustomerGroup)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 842, 845, Justification = Upstream.GaveNoReason)]
    public override Task Project_entity_with_complex_type_pushdown_and_then_left_join(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Project_entity_with_complex_type_pushdown_and_then_left_join(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);
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
            foreach (var entityType in modelBuilder.Model.GetEntityTypes()) RenameProperties(entityType);
        }
        private static void RenameProperties(IMutableTypeBase typeBase)
        {
            foreach (var property in typeBase.GetProperties())
                if (CosmosPropertyExtensions.GetJsonPropertyName(property) != "id") CosmosPropertyExtensions.SetJsonPropertyName(property, property.Name + "Renamed");
            foreach (var property in typeBase.GetComplexProperties())
            {
                CosmosComplexPropertyExtensions.SetJsonPropertyName(property, property.Name + "Renamed");
                RenameProperties(property.ComplexType);
            }
        }
    }
}
