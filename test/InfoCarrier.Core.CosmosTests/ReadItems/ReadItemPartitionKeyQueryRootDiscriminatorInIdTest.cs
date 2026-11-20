// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the pinned official EF Core 11 read-item/partition-key families.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications.ReadItems;

public class ReadItemPartitionKeyQueryRootDiscriminatorInIdTest(ReadItemPartitionKeyQueryRootDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture fixture)
    : ReadItemPartitionKeyQueryInheritanceTestBase<ReadItemPartitionKeyQueryRootDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ReadItemPartitionKeyQueryRootDiscriminatorInIdTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 88, 102, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 117, 131, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 213, 226, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 517, 531, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 533, 547, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 629, 642, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    public class ReadItemPartitionKeyQueryFixture : ReadItemPartitionKeyQueryInheritanceFixtureBase
    {
        protected override string StoreName
            => "PartitionKeyQueryRootDiscriminatorInIdTest";

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.HasRootDiscriminatorInJsonId();
        }
    }
}
