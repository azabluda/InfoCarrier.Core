// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public class CustomConvertersTest(CustomConvertersTest.CosmosFixture fixture)
    : CustomConvertersTestBase<CustomConvertersTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/CustomConvertersCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 14, 16, Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_filter_projection_with_inline_enum_variable(bool async) => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 18, 20, Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_filter_projection_with_captured_enum_variable(bool async) => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 22, 24, Skip = true, Justification = "Issue #16920")]
    public override Task Can_insert_and_read_back_with_string_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 26, 28, Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_query_and_update_with_conversion_for_custom_type() => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 30, 32, Skip = true, Justification = "Issue #16920")]
    public override Task Can_query_and_update_with_nullable_converter_on_primary_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 34, 36, Skip = true, Justification = "Issue #16920")]
    public override Task Can_insert_and_read_back_with_binary_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 38, 40, Skip = true, Justification = "Issue #16920")]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key() => throw SkipException.ForSkip("Issue #16920");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 42, 44, Skip = true, Justification = "Issue #17246 No Explicit Convert")]
    public override Task Can_insert_and_query_struct_to_string_converter_for_pk() => throw SkipException.ForSkip("Issue #17246 No Explicit Convert");

    [StoreIssue(IssueTracker.EfCore, 17670, UpstreamRepository.EfCore, Source, 46, 48, Skip = true, Justification = "Issue #17670")]
    public override Task Can_read_back_mapped_enum_from_collection_first_or_default() => throw SkipException.ForSkip("Issue #17670");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 50, 56, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Field_on_derived_type_retrieved_via_cast_applies_value_converter() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 58, 60, Skip = true, Justification = "Issue #17246")]
    public override Task Can_read_back_bool_mapped_as_int_through_navigation() => throw SkipException.ForSkip("Issue #17246");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 62, 64, Skip = true, Justification = "Issue #17246")]
    public override Task Value_conversion_is_appropriately_used_for_join_condition() => throw SkipException.ForSkip("Issue #17246");

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 66, 68, Skip = true, Justification = "Issue #17246")]
    public override Task Value_conversion_is_appropriately_used_for_left_join_condition() => throw SkipException.ForSkip("Issue #17246");

    [StoreDefect("1.21", UpstreamRepository.EfCore, Source, 122, 133, Justification = "Cosmos filters out the undefined value")]
    public override async Task Optional_owned_with_converter_reading_non_nullable_column()
    {
        // Cosmos filters out the undefined value
        var ex = await Assert.ThrowsAnyAsync<XunitException>(() => base.Optional_owned_with_converter_reading_non_nullable_column());
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 135, 138, Justification = Upstream.GaveNoReason)]
    public override void Value_conversion_on_enum_collection_contains()
        => Assert.Contains(
            CoreStrings.TranslationFailed("")[47..],
            Assert.Throws<InvalidOperationException>(base.Value_conversion_on_enum_collection_contains).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 140, 143, Justification = Upstream.GaveNoReason)]
    public override void GroupBy_converted_enum()
        => Assert.Contains(
            CoreStrings.TranslationFailed("")[21..],
            Assert.Throws<InvalidOperationException>(base.GroupBy_converted_enum).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 145, 146, Justification = Upstream.GaveNoReason)]
    public override void Infer_type_mapping_from_in_subquery_to_item()
        => Assert.Throws<InvalidOperationException>(base.Infer_type_mapping_from_in_subquery_to_item);

    [StoreLimit(UpstreamRepository.EfCore, Source, 148, 149, Justification = Upstream.GaveNoReason)]
    public override Task Can_query_custom_type_not_mapped_by_default_equality(bool async)
        => NoSyncTest(async, RunCustomTypeNotMappedByDefaultEqualityAsync);

    // The base test seeds a SimpleCounter and only cleans up after a successful query. When the
    // sync variant runs, .Single() throws SyncNotSupported before cleanup, leaving the seeded
    // entity behind and causing the next iteration to fail with a 409 conflict. Delete any
    // leftover rows before seeding to keep the test independent of execution order.
    private async Task RunCustomTypeNotMappedByDefaultEqualityAsync(bool async)
    {
        await using (var context = CreateContext())
        {
            var existing = await context.Set<SimpleCounter>().ToListAsync(TestContext.Current.CancellationToken);
            if (existing.Count > 0)
            {
                context.RemoveRange(existing);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
        }

        await base.Can_query_custom_type_not_mapped_by_default_equality(async);
    }

    [StoreLimit(typeof(CustomConverterControlTest), nameof(CustomConverterControlTest.Composition_over_converted_collection_refuses_translation),
        Justification = "Composition over a value-converted collection cannot be translated.")]
    [InfoCarrierDesign(Decisions.Limitations, "exception-message-text-for-an-untranslatable-query",
        Justification = "Carrier rewriting changes the diagnostic projection from an anonymous object to ValueTuple.",
        Deviation = DeviationKind.Other | DeviationKind.QueryWrittenOut,
        DeviationNote = "Execute the same query and assert its translation refusal; message text names the tuple carrier.")]
    public override void Composition_over_collection_of_complex_mapped_as_scalar()
    {
        using var context = CreateContext();
        var query = context.Set<Dashboard>().AsNoTracking().Select(d => new
        {
            d.Id,
            d.Name,
            Layouts = d.Layouts.Select(l => new { H = l.Height, W = l.Width }).ToList()
        });
        var error = Assert.Throws<InvalidOperationException>(() => query.ToList());
        Assert.StartsWith("The LINQ expression 'l => new ValueTuple<int, int>", error.Message);
        Assert.Contains("l.Height", error.Message, StringComparison.Ordinal);
        Assert.Contains("l.Width", error.Message, StringComparison.Ordinal);
        Assert.Contains("could not be translated", error.Message, StringComparison.Ordinal);
    }

    private static async Task NoSyncTest(bool async, Func<bool, Task> test)
    {
        if (async)
        {
            await test(true);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => test(false));
            Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", error.Message);
        }
    }

    public class CosmosFixture : CustomConvertersFixtureBase
    {
        private ITestStoreFactory? _factory;
        protected override bool UsePooling => false;
        protected override string StoreName => "CosmosCustomConverters";
        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating, configureConventions: ConfigureConventions, allowedTypes: [typeof(Layout)]);

        public override bool StrictEquality
            => true;

        public override int IntegerPrecision
            => 53;

        public override bool SupportsAnsi
            => false;

        public override bool SupportsUnicodeToAnsiConversion
            => false;

        public override bool SupportsLargeStringComparisons
            => true;

        public override bool SupportsBinaryKeys
            => true;

        public override bool SupportsDecimalComparisons
            => true;

        public override DateTime DefaultDateTime
            => new();

        public override bool PreservesDateTimeKind
            => true;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
                    => base.AddOptions(builder).ConfigureWarnings(w => w.Ignore(CosmosEventId.NoPartitionKeyDefined));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.HasDiscriminatorInJsonIds();

            modelBuilder.Entity<SimpleCounter>(b => b.ToContainer("SimpleCounters"));

            modelBuilder.Entity<Person>().Metadata.RemoveIndex(
                modelBuilder.Entity<Person>().Property(e => e.SSN).Metadata.GetContainingIndexes().Single());
        }
    }
}

