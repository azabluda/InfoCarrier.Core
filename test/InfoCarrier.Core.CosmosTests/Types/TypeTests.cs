// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Types;

// Values, mappings, and primitive-collection exclusions follow the pinned EF Core 11 Cosmos tests.
public abstract class DocumentTypeFixture<T> : TypeFixtureBase<T>
{
    private ITestStoreFactory? _factory;
    protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
        new CosmosTier(), typeof(DbContext), OnModelCreating,
        configureConventions: ConfigureConventions,
        configureServer: options => options.WithStoresOwnedDataInsideOwner(true));
}

public class BoolTypeTest(BoolTypeFixture fixture) : TypeTestBase<bool, BoolTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosMiscellaneousTypeTest.cs", 9, 15, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class BoolTypeFixture : DocumentTypeFixture<bool>
{
    public override bool Value { get; } = true;
    public override bool OtherValue { get; } = false;
}

public class StringTypeTest(StringTypeFixture fixture) : TypeTestBase<string, StringTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosMiscellaneousTypeTest.cs", 30, 36, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class StringTypeFixture : DocumentTypeFixture<string>
{
    public override string Value { get; } = "foo";
    public override string OtherValue { get; } = "bar";
}

public class GuidTypeTest(GuidTypeFixture fixture) : TypeTestBase<Guid, GuidTypeFixture>(fixture)
{
    [StoreLimit(UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosMiscellaneousTypeTest.cs", 51, 53, Skip = true,
        Justification = "Cosmos doesn't support value converters on primitive collection elements (Guid requires one)")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Cosmos doesn't support value converters on primitive collection elements (Guid requires one)");
}

public class GuidTypeFixture : DocumentTypeFixture<Guid>
{
    public override Guid Value { get; } = new("8f7331d6-cde9-44fb-8611-81fff686f280");
    public override Guid OtherValue { get; } = new("ae192c36-9004-49b2-b785-8be10d169627");

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        modelBuilder.Entity<TypeEntity<Guid>>().Ignore(entity => entity.ArrayValue);
    }
}

public class ByteArrayTypeTest(ByteArrayTypeFixture fixture) : TypeTestBase<byte[], ByteArrayTypeFixture>(fixture)
{
    [StoreLimit(UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosMiscellaneousTypeTest.cs", 74, 76, Skip = true,
        Justification = "Cosmos doesn't support value converters on primitive collection elements (byte[] requires one)")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Cosmos doesn't support value converters on primitive collection elements (byte[] requires one)");
}

public class ByteArrayTypeFixture : DocumentTypeFixture<byte[]>
{
    public override byte[] Value { get; } = [1, 2, 3];
    public override byte[] OtherValue { get; } = [4, 5, 6, 7];

    public override Func<byte[], byte[], bool> Comparer { get; } = (left, right) => left.SequenceEqual(right);

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);
        modelBuilder.Entity<TypeEntity<byte[]>>().Ignore(entity => entity.ArrayValue);
    }
}

public class ByteTypeTest(ByteTypeFixture fixture) : TypeTestBase<byte, ByteTypeFixture>(fixture)
{
    [StoreLimit(UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 9, 11, Skip = true,
        Justification = "Cosmos can't translate byte.Equals() in primitive collection queries")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Cosmos can't translate byte.Equals() in primitive collection queries");
}

public class ByteTypeFixture : DocumentTypeFixture<byte>
{
    public override byte Value { get; } = byte.MinValue;
    public override byte OtherValue { get; } = byte.MaxValue;
}

public class ShortTypeTest(ShortTypeFixture fixture) : TypeTestBase<short, ShortTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 26, 32, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class ShortTypeFixture : DocumentTypeFixture<short>
{
    public override short Value { get; } = short.MinValue;
    public override short OtherValue { get; } = short.MaxValue;
}

public class IntTypeTest(IntTypeFixture fixture) : TypeTestBase<int, IntTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 46, 52, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class IntTypeFixture : DocumentTypeFixture<int>
{
    public override int Value { get; } = int.MinValue;
    public override int OtherValue { get; } = int.MaxValue;
}

public class LongTypeTest(LongTypeFixture fixture) : TypeTestBase<long, LongTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 67, 73, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class LongTypeFixture : DocumentTypeFixture<long>
{
    public override long Value { get; } = long.MinValue;
    public override long OtherValue { get; } = long.MaxValue;
}

public class DecimalTypeTest(DecimalTypeFixture fixture) : TypeTestBase<decimal, DecimalTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 88, 94, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class DecimalTypeFixture : DocumentTypeFixture<decimal>
{
    public override decimal Value { get; } = 30.5m;
    public override decimal OtherValue { get; } = 30m;
}

public class DoubleTypeTest(DoubleTypeFixture fixture) : TypeTestBase<double, DoubleTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 109, 115, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class DoubleTypeFixture : DocumentTypeFixture<double>
{
    public override double Value { get; } = 30.5d;
    public override double OtherValue { get; } = 30d;
}

public class FloatTypeTest(FloatTypeFixture fixture) : TypeTestBase<float, FloatTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosNumericTypeTest.cs", 130, 136, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class FloatTypeFixture : DocumentTypeFixture<float>
{
    public override float Value { get; } = 30.5f;
    public override float OtherValue { get; } = 30f;
}

public class DateTimeTypeTest(DateTimeTypeFixture fixture) : TypeTestBase<DateTime, DateTimeTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosTemporalTypeTest.cs", 9, 15, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class DateTimeTypeFixture : DocumentTypeFixture<DateTime>
{
    public override DateTime Value { get; } = new(2020, 1, 5, 12, 30, 45, DateTimeKind.Unspecified);
    public override DateTime OtherValue { get; } = new(2022, 5, 3, 0, 0, 0, DateTimeKind.Unspecified);
}

public class DateTimeOffsetTypeTest(DateTimeOffsetTypeFixture fixture) : TypeTestBase<DateTimeOffset, DateTimeOffsetTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosTemporalTypeTest.cs", 30, 36, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class DateTimeOffsetTypeFixture : DocumentTypeFixture<DateTimeOffset>
{
    public override DateTimeOffset Value { get; } = new(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(2));
    public override DateTimeOffset OtherValue { get; } = new(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(3));
}

public class DateOnlyTypeTest(DateOnlyTypeFixture fixture) : TypeTestBase<DateOnly, DateOnlyTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosTemporalTypeTest.cs", 51, 57, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class DateOnlyTypeFixture : DocumentTypeFixture<DateOnly>
{
    public override DateOnly Value { get; } = new(2020, 1, 5);
    public override DateOnly OtherValue { get; } = new(2022, 5, 3);
}

public class TimeOnlyTypeTest(TimeOnlyTypeFixture fixture) : TypeTestBase<TimeOnly, TimeOnlyTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosTemporalTypeTest.cs", 72, 78, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class TimeOnlyTypeFixture : DocumentTypeFixture<TimeOnly>
{
    public override TimeOnly Value { get; } = new(12, 30, 45);
    public override TimeOnly OtherValue { get; } = new(14, 0, 0);
}

public class TimeSpanTypeTest(TimeSpanTypeFixture fixture) : TypeTestBase<TimeSpan, TimeSpanTypeFixture>(fixture)
{
    [StoreDefect("2.1", UpstreamRepository.EfCore,
        "test/EFCore.Cosmos.FunctionalTests/Types/CosmosTemporalTypeTest.cs", 93, 99, Skip = true,
        Justification = "Aggregates over subqueries return null result set")]
    public override Task Primitive_collection_in_query()
        => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
}

public class TimeSpanTypeFixture : DocumentTypeFixture<TimeSpan>
{
    public override TimeSpan Value { get; } = new(12, 30, 45);
    public override TimeSpan OtherValue { get; } = new(14, 0, 0);
}
