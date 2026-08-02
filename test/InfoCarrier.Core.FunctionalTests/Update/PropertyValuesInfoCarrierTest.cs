// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;
using Microsoft.Extensions.DependencyInjection;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Update;

/// <summary>
///     <c>PropertyValuesTestBase</c> on ADR-009 Tier A — 169 tests over current values, original
///     values, store values and <c>Reload</c>. This is the coverage the concurrency-token work
///     (S3c) needs: <c>SaveChangesRequest.SerializedOriginalValues</c> carries exactly what these
///     tests read.
/// </summary>
/// <remarks>
///     The overrides below are EF Core's own <c>PropertyValuesInMemoryTest</c> overrides,
///     mirrored one for one — the backend <em>is</em> the InMemory store, so complex types and
///     complex collections are as unsupported here as they are there. Re-test each against
///     Tier B and delete it where it passes (roadmap M3).
/// </remarks>
public class PropertyValuesInfoCarrierTest(PropertyValuesInfoCarrierTest.InfoCarrierFixture fixture)
    : PropertyValuesTestBase<PropertyValuesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public override Task Complex_current_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_current_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_original_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_original_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_store_values_can_be_accessed_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_store_values_can_be_accessed_as_a_property_dictionary_using_IProperty());

    public override Task Complex_store_values_can_be_accessed_asynchronously_as_a_property_dictionary_using_IProperty()
        => Assert.ThrowsAsync<NullReferenceException>( // In-memory database cannot query complex types
            () => base.Complex_store_values_can_be_accessed_asynchronously_as_a_property_dictionary_using_IProperty());

    public override Task Values_can_be_reloaded_from_database_for_entity_in_any_state_with_inheritance(EntityState state, bool async)
        => Assert.ThrowsAnyAsync<Exception>( // In-memory database cannot query complex types
            () => base.Values_can_be_reloaded_from_database_for_entity_in_any_state_with_inheritance(state, async));

    // Complex collection tests - InMemory provider doesn't support complex collections yet
    public override Task Complex_collection_current_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Complex_collection_current_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_original_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Complex_collection_original_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_store_values_can_be_accessed_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Complex_collection_store_values_can_be_accessed_as_a_property_dictionary());

    public override Task Complex_collection_store_values_can_be_accessed_asynchronously_as_a_property_dictionary()
        => Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Complex_collection_store_values_can_be_accessed_asynchronously_as_a_property_dictionary());

    public override void Setting_complex_collection_values_from_object_works()
        => Assert.Throws<InvalidOperationException>(() => base.Setting_complex_collection_values_from_object_works());

    public override void Setting_complex_collection_original_values_from_object_with_nulls_works()
        => Assert.Throws<InvalidOperationException>(() => base.Setting_complex_collection_original_values_from_object_with_nulls_works());

    public override void Setting_complex_collection_original_values_from_dictionary_with_nulls_works()
        => Assert.Throws<InvalidOperationException>(()
            => base.Setting_complex_collection_original_values_from_dictionary_with_nulls_works());

    public override void Setting_complex_collection_current_values_from_dictionary_works()
        => Assert.Throws<InvalidOperationException>(() => base.Setting_complex_collection_current_values_from_dictionary_works());

    public override void SetValues_throws_for_nested_complex_collection_with_non_list_value()
        => Assert.Throws<InvalidOperationException>(() => base.SetValues_throws_for_nested_complex_collection_with_non_list_value());

    public override void SetValues_throws_for_complex_property_with_non_dictionary_value()
        => Assert.Throws<ThrowsException>(() => base.SetValues_throws_for_complex_property_with_non_dictionary_value());

    public override void SetValues_throws_for_complex_collection_with_non_list_value()
        => Assert.Throws<InvalidOperationException>(() => base.SetValues_throws_for_complex_collection_with_non_list_value());

    public override void SetValues_throws_for_complex_collection_with_non_dictionary_item()
        => Assert.Throws<InvalidOperationException>(() => base.SetValues_throws_for_complex_collection_with_non_dictionary_item());

    public override void SetValues_throws_for_nested_complex_collection_with_non_dictionary_item()
        => Assert.Throws<InvalidOperationException>(() => base.SetValues_throws_for_nested_complex_collection_with_non_dictionary_item());

    public override void Setting_complex_collection_current_values_from_DTO_with_complex_metadata_access_works()
        => Assert.Throws<InvalidOperationException>(()
            => base.Setting_complex_collection_current_values_from_DTO_with_complex_metadata_access_works());

    public override void Setting_complex_collection_values_from_DTO_with_nulls_works()
        => Assert.Throws<InvalidOperationException>(() => base.Setting_complex_collection_values_from_DTO_with_nulls_works());

    public override void Setting_complex_collection_current_values_from_dictionary_with_nulls_works()
        => Assert.Throws<InvalidOperationException>(()
            => base.Setting_complex_collection_current_values_from_dictionary_with_nulls_works());

    public override void Setting_complex_collection_current_values_from_object_with_nulls_works()
        => Assert.Throws<InvalidOperationException>(() => base.Setting_complex_collection_current_values_from_object_with_nulls_works());

    public override void Using_complex_property_value_not_list_throws()
        => Assert.Throws<InvalidOperationException>(() => base.Using_complex_property_value_not_list_throws());

    public override void Using_non_collection_complex_property_throws()
        => Assert.Throws<ThrowsException>(() => base.Using_non_collection_complex_property_throws());

    public class InfoCarrierFixture : PropertyValuesFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),

                // The seed asserts that the materialization interceptor ran, and the seed runs
                // against the server — so the server provider needs it too.
                onAddServices: services =>
                    services.AddSingleton<ISingletonInterceptor, PropertyValuesMaterializationInterceptor>());

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Ignore(CoreEventId.MappedComplexPropertyIgnoredWarning)
                    .Ignore(CoreEventId.MappedEntityTypeIgnoredWarning))
                .EnableSensitiveDataLogging(false);

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // In-memory database doesn't support complex type queries
            modelBuilder.Entity<Building>(b =>
            {
                b.Ignore(e => e.Culture);
                b.Ignore(e => e.Milk);
            });

            // In-memory database doesn't support complex collections
            modelBuilder.Ignore<School>();
        }
    }
}
