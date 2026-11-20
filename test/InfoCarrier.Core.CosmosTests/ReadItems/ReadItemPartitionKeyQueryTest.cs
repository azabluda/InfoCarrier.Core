// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from the pinned official EF Core 11 read-item/partition-key families.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications.ReadItems;

public class ReadItemPartitionKeyQueryTest(ReadItemPartitionKeyQueryTest.ReadItemPartitionKeyQueryFixture fixture)
    : ReadItemPartitionKeyQueryTestBase<ReadItemPartitionKeyQueryTest.ReadItemPartitionKeyQueryFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/ReadItemPartitionKeyQueryTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 90, 104, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 119, 133, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task Predicate_with_partial_values_in_only_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 211, 224, Skip = true,
        Justification = "Linux emulator issue 329: partial hierarchical partition key queries return too many results.")]
    public override Task WithPartitionKey_with_partial_value_in_hierarchical_partition_key()
        => throw SkipException.ForSkip("Linux emulator issue 329: partial hierarchical partition key queries return too many results.");

    public class ReadItemPartitionKeyQueryFixture : ReadItemPartitionKeyQueryFixtureBase
    {
        protected override string StoreName
            => "ReadItemPartitionKeyQueryTest";

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(
                builder.ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined)));
    }
}
