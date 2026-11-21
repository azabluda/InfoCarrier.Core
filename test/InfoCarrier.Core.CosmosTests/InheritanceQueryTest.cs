// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Inheritance;
using Microsoft.EntityFrameworkCore.TestModels.InheritanceModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests.Specifications;

#pragma warning disable EF1001 // Assert official Cosmos diagnostic resources.
public class InheritanceQueryTest(InheritanceQueryTest.CosmosFixture fixture)
    : InheritanceQueryTestBase<InheritanceQueryTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/Inheritance/InheritanceQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 21, 26, Justification = Upstream.GaveNoReason)]
    public override async Task Using_OfType_on_multiple_type_with_no_result(bool async)
    {
        await base.Using_OfType_on_multiple_type_with_no_result(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 28, 55, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_when_shared_column(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_when_shared_column(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 57, 69, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_all_types_when_shared_column(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_all_types_when_shared_column(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 71, 84, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_animal(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_animal(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 86, 98, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_is_kiwi(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_is_kiwi(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 100, 112, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_is_kiwi_with_cast(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_is_kiwi_with_cast(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 114, 126, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_backwards_is_animal(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_backwards_is_animal(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 128, 140, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_is_kiwi_with_other_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_is_kiwi_with_other_predicate(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 142, 154, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_is_kiwi_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_is_kiwi_in_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 156, 169, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_bird(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_bird(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 171, 184, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_bird_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_bird_predicate(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 186, 198, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_bird_with_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_bird_with_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 200, 214, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_bird_first(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_bird_first(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 216, 228, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_kiwi(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_kiwi(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 230, 242, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_backwards_of_type_animal(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_backwards_of_type_animal(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 244, 256, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_rose(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_rose(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 258, 271, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_all_animals(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_all_animals(a);


            });

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 273, 279, Skip = true, Justification = "Issue#17246 Views are not supported")]
    [Theory(Skip = "Issue#17246 Views are not supported")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Can_query_all_animal_views(bool async)
    {
        await base.Can_query_all_animal_views(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 281, 294, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_all_plants(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_all_plants(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 296, 309, Justification = Upstream.GaveNoReason)]
    public override Task Can_filter_all_animals(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_filter_all_animals(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 311, 324, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_all_birds(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_all_birds(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 326, 339, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_just_kiwis(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_just_kiwis(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 341, 354, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_just_roses(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_query_just_roses(a);


            });

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 356, 362, Skip = true, Justification = "Issue#17246 Non-embedded Include")]
    [Theory(Skip = "Issue#17246 Non-embedded Include")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Can_include_animals(bool async)
    {
        await base.Can_include_animals(async);


    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 364, 370, Skip = true, Justification = "Issue#17246 Non-embedded Include")]
    [Theory(Skip = "Issue#17246 Non-embedded Include")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Can_include_prey(bool async)
    {
        await base.Can_include_prey(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 372, 384, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_kiwi_where_south_on_derived_property(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_kiwi_where_south_on_derived_property(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 386, 398, Justification = Upstream.GaveNoReason)]
    public override Task Can_use_of_type_kiwi_where_north_on_derived_property(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Can_use_of_type_kiwi_where_north_on_derived_property(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 400, 412, Justification = Upstream.GaveNoReason)]
    public override Task Discriminator_used_when_projection_over_derived_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Discriminator_used_when_projection_over_derived_type(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 414, 430, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Discriminator_used_when_projection_over_derived_type2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Discriminator_used_when_projection_over_derived_type2(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 432, 444, Justification = Upstream.GaveNoReason)]
    public override Task Discriminator_with_cast_in_shadow_property(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Discriminator_with_cast_in_shadow_property(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 446, 458, Justification = Upstream.GaveNoReason)]
    public override Task Discriminator_used_when_projection_over_of_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Discriminator_used_when_projection_over_of_type(a);


            });

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 460, 466, Skip = true, Justification = "Issue#17246 Transations not supported")]
    [Fact(Skip = "Issue#17246 Transations not supported")]
    public override async Task Can_insert_update_delete()
    {
        await base.Can_insert_update_delete();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 468, 473, Justification = Upstream.GaveNoReason)]
    public override async Task Union_siblings_with_duplicate_property_in_subquery(bool async)
    {
        await base.Union_siblings_with_duplicate_property_in_subquery(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 475, 480, Justification = Upstream.GaveNoReason)]
    public override async Task OfType_Union_subquery(bool async)
    {
        await base.OfType_Union_subquery(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 482, 487, Justification = Upstream.GaveNoReason)]
    public override async Task OfType_Union_OfType(bool async)
    {
        await base.OfType_Union_OfType(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 489, 492, Justification = Upstream.GaveNoReason)]
    public override Task Subquery_OfType(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Subquery_OfType(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 494, 499, Justification = Upstream.GaveNoReason)]
    public override async Task Union_entity_equality(bool async)
    {
        await base.Union_entity_equality(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 501, 512, Justification = Upstream.GaveNoReason)]
    public override async Task Setting_foreign_key_to_a_different_type_throws()
    {
        await base.Setting_foreign_key_to_a_different_type_throws();


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 514, 526, Justification = Upstream.GaveNoReason)]
    public override Task Byte_enum_value_constant_used_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Byte_enum_value_constant_used_in_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 528, 539, Justification = Upstream.GaveNoReason)]
    public override async Task Member_access_on_intermediate_type_works()
    {
        await base.Member_access_on_intermediate_type_works();


    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 541, 547, Skip = true, Justification = "Issue#17246 subquery usage")]
    [Theory(Skip = "Issue#17246 subquery usage")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Is_operator_on_result_of_FirstOrDefault(bool async)
    {
        await base.Is_operator_on_result_of_FirstOrDefault(async);


    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 549, 561, Justification = Upstream.GaveNoReason)]
    public override Task Selecting_only_base_properties_on_base_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Selecting_only_base_properties_on_base_type(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 563, 575, Justification = Upstream.GaveNoReason)]
    public override Task Selecting_only_base_properties_on_derived_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Selecting_only_base_properties_on_derived_type(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 577, 589, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_abstract_base_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_abstract_base_type(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 591, 603, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_intermediate_type(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_intermediate_type(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 605, 617, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 619, 631, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 633, 645, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2_reverse(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2_reverse(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 647, 659, Justification = Upstream.GaveNoReason)]
    public override Task GetType_in_hierarchy_in_leaf_type_with_sibling2_not_equal(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.GetType_in_hierarchy_in_leaf_type_with_sibling2_not_equal(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 661, 673, Justification = Upstream.GaveNoReason)]
    public override Task Using_is_operator_on_multiple_type_with_no_result(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Using_is_operator_on_multiple_type_with_no_result(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 675, 687, Justification = Upstream.GaveNoReason)]
    public override Task Using_is_operator_with_of_type_on_multiple_type_with_no_result(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Using_is_operator_with_of_type_on_multiple_type_with_no_result(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 689, 701, Justification = Upstream.GaveNoReason)]
    public override Task Primitive_collection_on_subtype(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Primitive_collection_on_subtype(a);


            });
    protected override bool EnforcesFkConstraints => false;

    public class CosmosFixture : InheritanceQueryFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, onAddOptions: AddOptions,
            configureConventions: ConfigureConventions);
        public override bool UseGeneratedKeys => false;
        public override bool EnableComplexTypes => false;
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder.ConfigureWarnings(w => w.Ignore(
                CoreEventId.MappedEntityTypeIgnoredWarning, CosmosEventId.NoPartitionKeyDefined)));
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.Entity<Animal>().ToContainer("Animals");
            modelBuilder.Entity<Plant>().ToContainer("Plants");
            modelBuilder.Entity<Microsoft.EntityFrameworkCore.TestModels.InheritanceModel.Country>().ToContainer("Countries");
            modelBuilder.Entity<Drink>().ToContainer("Drinks");
            modelBuilder.Entity<KiwiQuery>().ToContainer("Animals");
            modelBuilder.Entity<AnimalQuery>().ToContainer("Animals");
            modelBuilder.Entity<BirdQuery>().ToContainer("Animals");
        }
        public async Task NoSyncTest(bool async, Func<bool, Task> test)
        {
            if (async) await test(true);
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
                Assert.Equal(CosmosStrings.SyncNotSupported, error.Message);
            }
        }
    }
}

