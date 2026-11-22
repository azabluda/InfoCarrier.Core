// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the pinned official EF Core 11 read-item/partition-key families.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications.ReadItems;

public class ReadItemPartitionKeyQueryNoDiscriminatorInIdTest(ReadItemPartitionKeyQueryNoDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture fixture)
    : ReadItemPartitionKeyQueryInheritanceTestBase<ReadItemPartitionKeyQueryNoDiscriminatorInIdTest.ReadItemPartitionKeyQueryFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ReadItemPartitionKeyQueryNoDiscriminatorInIdTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 97, 111, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 126, 140, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 220, 233, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 524, 538, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 540, 554, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 634, 647, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key_leaf()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    public class ReadItemPartitionKeyQueryFixture : ReadItemPartitionKeyQueryInheritanceFixtureBase
    {
        protected override string StoreName
            => "PartitionKeyQueryNoDiscriminatorInIdTest";
    }
}
