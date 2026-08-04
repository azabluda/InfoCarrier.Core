// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestModels;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>KeysWithConvertersTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A key behind a value converter is the case every identity path in this provider has to get
///     right twice: the client resolves identity from the key array it decoded, and the server
///     re-keys the same row from the wire. `ValueConvertersEndToEnd` covers converters on ordinary
///     properties; this is the same question where the property is the thing rows are found by.
/// </remarks>
public class KeysWithConvertersInfoCarrierTest(KeysWithConvertersInfoCarrierTest.InfoCarrierFixture fixture)
    : KeysWithConvertersTestBase<KeysWithConvertersInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    // EF's own `KeysWithConvertersInMemoryTest` skips, mirrored one for one: the backend *is*
    // InMemory, so its limits are ours. Issue #26238.
    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_insert_and_read_back_with_bare_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_bare_class_key_and_optional_dependents();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_insert_and_read_back_with_bare_class_key_and_optional_dependents_with_shadow_FK()
        => base.Can_insert_and_read_back_with_bare_class_key_and_optional_dependents_with_shadow_FK();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_struct_binary_key_and_optional_dependents();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_insert_and_read_back_with_struct_binary_key_and_required_dependents()
        => base.Can_insert_and_read_back_with_struct_binary_key_and_required_dependents();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_query_and_update_owned_entity_with_value_converter()
        => base.Can_query_and_update_owned_entity_with_value_converter();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_query_and_update_owned_entity_with_int_bare_class_key()
        => base.Can_query_and_update_owned_entity_with_int_bare_class_key();

    [ConditionalFact(Skip = "Issue #26238")]
    public override Task Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents()
        => base.Can_insert_and_read_back_with_enumerable_class_key_and_optional_dependents();

    public class InfoCarrierFixture : KeysWithConvertersFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder.ConfigureWarnings(w => w.Ignore(CoreEventId.MappedEntityTypeIgnoredWarning)));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // Issue #26238, as EF's own InMemory fixture does.
            modelBuilder.Ignore<EnumerableClassKeyPrincipal>();
            modelBuilder.Ignore<EnumerableClassKeyOptionalDependent>();
            modelBuilder.Ignore<EnumerableClassKeyRequiredDependent>();
        }
    }
}
