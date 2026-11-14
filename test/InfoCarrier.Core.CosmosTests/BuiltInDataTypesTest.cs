// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public class BuiltInDataTypesTest(BuiltInDataTypesTest.CosmosFixture fixture)
    : BuiltInDataTypesTestBase<BuiltInDataTypesTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/BuiltInDataTypesCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 9, 11,
        Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_filter_projection_with_inline_enum_variable(bool async) => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 13, 15,
        Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_filter_projection_with_captured_enum_variable(bool async) => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 17, 19,
        Skip = true, Justification = "Issue #16920")]
    public override Task Can_insert_and_read_back_with_string_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 21, 23,
        Skip = true, Justification = "Issue #16920")]
    public override Task Can_insert_and_read_back_with_binary_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 25, 27,
        Skip = true, Justification = "Better translation of sequential equality #17246")]
    public override Task Can_perform_query_with_max_length() => throw SkipException.ForSkip("Better translation of sequential equality #17246");

    [StoreIssue(IssueTracker.EfCore, 17670, UpstreamRepository.EfCore, Source, 29, 31,
        Skip = true, Justification = "Issue #17670")]
    public override Task Can_read_back_mapped_enum_from_collection_first_or_default() => throw SkipException.ForSkip("Issue #17670");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 33, 35,
        Skip = true, Justification = "Issue #17246")]
    public override Task Can_read_back_bool_mapped_as_int_through_navigation() => throw SkipException.ForSkip("Issue #17246");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 37, 50,
        Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Object_to_string_conversion() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    public class CosmosFixture : BuiltInDataTypesFixtureBase
    {
        private ITestStoreFactory? _factory;

        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating,
            configureConventions: ConfigureConventions,
            configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

        public override bool StrictEquality => true;
        public override int IntegerPrecision => 53;
        public override bool SupportsAnsi => false;
        public override bool SupportsUnicodeToAnsiConversion => false;
        public override bool SupportsLargeStringComparisons => true;
        public override bool SupportsBinaryKeys => true;
        public override bool SupportsDecimalComparisons => true;
        public override DateTime DefaultDateTime => new();
        public override bool PreservesDateTimeKind => true;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder).ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            modelBuilder.HasDiscriminatorInJsonIds();
        }
    }
}
