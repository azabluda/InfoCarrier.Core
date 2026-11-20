// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the pinned official EF Core 11 read-item/partition-key families.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications.ReadItems;

public class ReadItemPartitionKeyQueryDiscriminatorInIdTest(ReadItemPartitionKeyQueryDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture fixture)
    : ReadItemPartitionKeyQueryInheritanceTestBase<ReadItemPartitionKeyQueryDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ReadItemPartitionKeyQueryDiscriminatorInIdTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 100, 114, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 129, 143, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 225, 238, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 646, 660, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 662, 676, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 758, 771, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    public class ReadItemPartitionKeyQueryFixture : ReadItemPartitionKeyQueryInheritanceFixtureBase
    {
        protected override string StoreName
            => "PartitionKeyQueryDiscriminatorInIdTest";

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.HasDiscriminatorInJsonIds();
        }
    }
}
