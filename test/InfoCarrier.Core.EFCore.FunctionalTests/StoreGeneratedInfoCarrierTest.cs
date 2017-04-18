namespace InfoCarrier.Core.EFCore.FunctionalTests
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Specification.Tests;
    using Xunit;

    public class StoreGeneratedInfoCarrierTest
        : StoreGeneratedTestBase<TestStore, StoreGeneratedInfoCarrierTest.StoreGeneratedInfoCarrierFixture>
    {
        public StoreGeneratedInfoCarrierTest(StoreGeneratedInfoCarrierFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public override void Identity_key_with_read_only_before_save_throws_if_explicit_values_set()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Identity_property_on_Added_entity_with_temporary_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Identity_property_on_Added_entity_with_default_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Identity_property_on_Modified_entity_with_read_only_after_save_throws_if_value_is_in_modified_state()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Identity_property_on_Modified_entity_is_included_in_update_when_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Identity_property_on_Modified_entity_is_not_included_in_update_when_not_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Computed_property_on_Added_entity_with_temporary_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Computed_property_on_Added_entity_with_default_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Computed_property_on_Modified_entity_with_read_only_after_save_throws_if_value_is_in_modified_state()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Computed_property_on_Modified_entity_is_included_in_update_when_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Computed_property_on_Modified_entity_is_read_from_store_when_not_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Added_entity_with_temporary_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Added_entity_with_default_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Added_entity_with_read_only_before_save_throws_if_explicit_values_set()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Added_entity_cannot_have_value_set_explicitly()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Modified_entity_with_read_only_after_save_throws_if_value_is_in_modified_state()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Modified_entity_is_not_included_in_update_even_when_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_computed_property_on_Modified_entity_is_read_from_store_when_not_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Added_entity_with_temporary_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Added_entity_with_default_value_gets_value_from_store()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Added_entity_with_read_only_before_save_throws_if_explicit_values_set()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Added_entity_gets_store_value_even_when_set_explicitly()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Modified_entity_with_read_only_after_save_throws_if_value_is_in_modified_state()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Modified_entity_is_not_included_in_update_when_modified()
        {
            // In-memory store does not support store generation
        }

        [Fact]
        public override void Always_identity_property_on_Modified_entity_is_not_included_in_the_update_when_not_modified()
        {
            // In-memory store does not support store generation
        }

        public class StoreGeneratedInfoCarrierFixture : StoreGeneratedFixtureBase
        {
            private readonly InfoCarrierInMemoryTestHelper<StoreGeneratedContext> helper;

            public StoreGeneratedInfoCarrierFixture()
            {
                this.helper = InfoCarrierTestHelper.CreateInMemory(
                    this.OnModelCreating,
                    (opt, _) => new StoreGeneratedContext(opt),
                    w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            }

            public override TestStore CreateTestStore()
                => this.helper.CreateTestStore(_ => { });

            public override DbContext CreateContext(TestStore testStore)
                => this.helper.CreateInfoCarrierContext();

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);

                modelBuilder.Entity<Gumball>(b =>
                {
                    // In-memory store does not support store generationof keys
                    b.Property(e => e.Id).Metadata.IsReadOnlyBeforeSave = false;
                });
            }
        }
    }
}