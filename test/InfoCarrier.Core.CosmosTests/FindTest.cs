// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public abstract class FindTest(FindTest.FindFixture fixture) : FindTestBase<FindTest.FindFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/FindCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 25886, UpstreamRepository.EfCore, Source, 12, 13, Skip = true, Justification = "#25886")]
    public override void Find_base_type_using_derived_set_tracked() => throw SkipException.ForSkip("#25886");

    [StoreIssue(IssueTracker.EfCore, 25886, UpstreamRepository.EfCore, Source, 15, 17, Skip = true, Justification = "#25886")]
    public override Task Find_base_type_using_derived_set_tracked_async(CancellationType cancellationType) => throw SkipException.ForSkip("#25886");

    [StoreIssue(IssueTracker.EfCore, 25886, UpstreamRepository.EfCore, Source, 19, 20, Skip = true, Justification = "#25886")]
    public override void Find_derived_using_base_set_type_from_store() => throw SkipException.ForSkip("#25886");

    [StoreIssue(IssueTracker.EfCore, 25886, UpstreamRepository.EfCore, Source, 22, 24, Skip = true, Justification = "#25886")]
    public override Task Find_derived_using_base_set_type_from_store_async(CancellationType cancellationType) => throw SkipException.ForSkip("#25886");

    [StoreLimit(UpstreamRepository.EfCore, Source, 26, 27,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_int_key_from_store()
        => AssertNoSync(base.Find_int_key_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 29, 30,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_int_key_not_in_store()
        => AssertNoSync(base.Returns_null_for_int_key_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 32, 33,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_nullable_int_key_from_store()
        => AssertNoSync(base.Find_nullable_int_key_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 35, 36,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_nullable_int_key_not_in_store()
        => AssertNoSync(base.Returns_null_for_nullable_int_key_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 38, 39,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_string_key_from_store()
        => AssertNoSync(base.Find_string_key_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 41, 42,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_string_key_not_in_store()
        => AssertNoSync(base.Returns_null_for_string_key_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 44, 45,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_composite_key_from_store()
        => AssertNoSync(base.Find_composite_key_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 47, 48,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_composite_key_not_in_store()
        => AssertNoSync(base.Returns_null_for_composite_key_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 50, 51,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_base_type_from_store()
        => AssertNoSync(base.Find_base_type_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 53, 54,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_base_type_not_in_store()
        => AssertNoSync(base.Returns_null_for_base_type_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 56, 57,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_derived_type_from_store()
        => AssertNoSync(base.Find_derived_type_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 59, 60,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_derived_type_not_in_store()
        => AssertNoSync(base.Returns_null_for_derived_type_not_in_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 62, 63,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_base_type_using_derived_set_from_store()
        => AssertNoSync(base.Find_base_type_using_derived_set_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 65, 66,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Find_shadow_key_from_store()
        => AssertNoSync(base.Find_shadow_key_from_store);

    [StoreLimit(UpstreamRepository.EfCore, Source, 68, 69,
        Justification = "Cosmos does not support synchronous operations.")]
    public override void Returns_null_for_shadow_key_not_in_store()
        => AssertNoSync(base.Returns_null_for_shadow_key_not_in_store);

    private static void AssertNoSync(Action action)
        => Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", Assert.Throws<InvalidOperationException>(action).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 323, 331,
        Justification = "Unable to materialize entity instance of type 'DerivedType'. No discriminators matched the discriminator value 'BaseType'.")]
    public override async Task Find_base_type_using_derived_set_from_store_async(CancellationType cancellationType)
        => Assert.Equal(
            "Unable to materialize entity instance of type 'DerivedType'. No discriminators matched the discriminator value 'BaseType'.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() =>
                base.Find_base_type_using_derived_set_from_store_async(cancellationType))).Message);

    public class ViaSet(FindFixture fixture) : FindTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaSetFinder();
    }

    public class ViaContext(FindFixture fixture) : FindTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaContextFinder();
    }

    public class ViaNonGenericContext(FindFixture fixture) : FindTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaNonGenericContextFinder();
    }

    public class FindFixture : FindFixtureBase
    {
        private ITestStoreFactory? _factory;

        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating,
            configureConventions: ConfigureConventions,
            configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.Entity<IntKey>().ToContainer("Ints").HasRootDiscriminatorInJsonId();
            modelBuilder.Entity<NullableIntKey>().ToContainer("Ints").HasRootDiscriminatorInJsonId();
            modelBuilder.Entity<StringKey>().ToContainer("Strings");
            modelBuilder.Entity<CompositeKey>().ToContainer("CompositeKeys");
            modelBuilder.Entity<BaseType>().ToContainer("Base").HasRootDiscriminatorInJsonId();
            modelBuilder.Entity<ShadowKey>().ToContainer("ShadowKeys");
        }
    }
}
