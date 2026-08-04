// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Types;

namespace InfoCarrier.Core.FunctionalTests.Types;

/// <summary>
///     The shared fixture for <c>TypeTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         One entity, one property, one value, one query — asked once per CLR type. It is a thin
///         base and that is the point: it isolates "does a value of this type survive the round
///         trip and still compare equal in a query" from every other thing a query can get wrong.
///         B4 is the argument for having it — a whole family of wire defects turned out to be
///         per-type, and nothing in the suite asked the question one type at a time.
///     </para>
///     <para>
///         <b>Tier B</b>: EF ships <c>SqliteMiscellaneousTypeTest</c>,
///         <c>SqliteNumericTypeTest</c> and <c>SqliteTemporalTypeTest</c>, and no InMemory
///         counterpart — a store that keeps live CLR objects cannot fail a type round trip and so
///         cannot test one. The sixteen types below are exactly the ones EF's SQLite suite covers.
///     </para>
///     <para>
///         The <em>core</em> base, not <c>RelationalTypeTestBase</c>: that one lives in
///         <c>EFCore.Relational.Specification.Tests</c>, which this project does not reference
///         (A79 mirrors its overrides by hand for the same reason), and its extra tests assert
///         JSON columns and <c>ExecuteUpdate</c> — neither of which a client with no database has.
///     </para>
///     <para>
///         <b>A store name per type.</b> EF's fixture names them all <c>TypeTest</c> and relies on
///         <c>[Collection("Type tests")]</c> to keep them apart in time. The Tier B store is
///         file-backed, and a shared file whose contents depend on class ordering is the exact
///         coupling that produced a 698-test phantom failure once already.
///     </para>
/// </remarks>
public abstract class TypeInfoCarrierFixture<T> : TypeFixtureBase<T>
    where T : notnull
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override string StoreName
        => "TypeInfoCarrierTest" + new string([.. typeof(T).Name.Where(char.IsLetterOrDigit)]);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.Sqlite,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}

public class BoolTypeInfoCarrierTest(BoolTypeInfoCarrierTest.BoolTypeInfoCarrierFixture fixture)
    : TypeTestBase<bool, BoolTypeInfoCarrierTest.BoolTypeInfoCarrierFixture>(fixture)
{
    public class BoolTypeInfoCarrierFixture : TypeInfoCarrierFixture<bool>
    {
        public override bool Value { get; } = true;

        public override bool OtherValue { get; }
    }
}

public class StringTypeInfoCarrierTest(StringTypeInfoCarrierTest.StringTypeInfoCarrierFixture fixture)
    : TypeTestBase<string, StringTypeInfoCarrierTest.StringTypeInfoCarrierFixture>(fixture)
{
    public class StringTypeInfoCarrierFixture : TypeInfoCarrierFixture<string>
    {
        public override string Value { get; } = "foo";

        public override string OtherValue { get; } = "bar";
    }
}

public class GuidTypeInfoCarrierTest(GuidTypeInfoCarrierTest.GuidTypeInfoCarrierFixture fixture)
    : TypeTestBase<Guid, GuidTypeInfoCarrierTest.GuidTypeInfoCarrierFixture>(fixture)
{
    public class GuidTypeInfoCarrierFixture : TypeInfoCarrierFixture<Guid>
    {
        public override Guid Value { get; } = new("8f7331d6-cde9-44fb-8611-81fff686f280");

        public override Guid OtherValue { get; } = new("ae192c36-9004-49b2-b785-8be10d169627");
    }
}

public class ByteArrayTypeInfoCarrierTest(ByteArrayTypeInfoCarrierTest.ByteArrayTypeInfoCarrierFixture fixture)
    : TypeTestBase<byte[], ByteArrayTypeInfoCarrierTest.ByteArrayTypeInfoCarrierFixture>(fixture)
{
    public class ByteArrayTypeInfoCarrierFixture : TypeInfoCarrierFixture<byte[]>
    {
        public override byte[] Value { get; } = [1, 2, 3];

        public override byte[] OtherValue { get; } = [4, 5, 6, 7];

        public override Func<byte[], byte[], bool> Comparer { get; } = (a, b) => a.SequenceEqual(b);
    }
}

public class ByteTypeInfoCarrierTest(ByteTypeInfoCarrierTest.ByteTypeInfoCarrierFixture fixture)
    : TypeTestBase<byte, ByteTypeInfoCarrierTest.ByteTypeInfoCarrierFixture>(fixture)
{
    public class ByteTypeInfoCarrierFixture : TypeInfoCarrierFixture<byte>
    {
        public override byte Value { get; } = byte.MinValue;

        public override byte OtherValue { get; } = byte.MaxValue;
    }
}

public class ShortTypeInfoCarrierTest(ShortTypeInfoCarrierTest.ShortTypeInfoCarrierFixture fixture)
    : TypeTestBase<short, ShortTypeInfoCarrierTest.ShortTypeInfoCarrierFixture>(fixture)
{
    public class ShortTypeInfoCarrierFixture : TypeInfoCarrierFixture<short>
    {
        public override short Value { get; } = short.MinValue;

        public override short OtherValue { get; } = short.MaxValue;
    }
}

public class IntTypeInfoCarrierTest(IntTypeInfoCarrierTest.IntTypeInfoCarrierFixture fixture)
    : TypeTestBase<int, IntTypeInfoCarrierTest.IntTypeInfoCarrierFixture>(fixture)
{
    public class IntTypeInfoCarrierFixture : TypeInfoCarrierFixture<int>
    {
        public override int Value { get; } = int.MinValue;

        public override int OtherValue { get; } = int.MaxValue;
    }
}

public class LongTypeInfoCarrierTest(LongTypeInfoCarrierTest.LongTypeInfoCarrierFixture fixture)
    : TypeTestBase<long, LongTypeInfoCarrierTest.LongTypeInfoCarrierFixture>(fixture)
{
    public class LongTypeInfoCarrierFixture : TypeInfoCarrierFixture<long>
    {
        public override long Value { get; } = long.MinValue;

        public override long OtherValue { get; } = long.MaxValue;
    }
}

public class DecimalTypeInfoCarrierTest(DecimalTypeInfoCarrierTest.DecimalTypeInfoCarrierFixture fixture)
    : TypeTestBase<decimal, DecimalTypeInfoCarrierTest.DecimalTypeInfoCarrierFixture>(fixture)
{
    public class DecimalTypeInfoCarrierFixture : TypeInfoCarrierFixture<decimal>
    {
        public override decimal Value { get; } = 30.5m;

        public override decimal OtherValue { get; } = 30m;
    }
}

public class DoubleTypeInfoCarrierTest(DoubleTypeInfoCarrierTest.DoubleTypeInfoCarrierFixture fixture)
    : TypeTestBase<double, DoubleTypeInfoCarrierTest.DoubleTypeInfoCarrierFixture>(fixture)
{
    public class DoubleTypeInfoCarrierFixture : TypeInfoCarrierFixture<double>
    {
        public override double Value { get; } = 30.5d;

        public override double OtherValue { get; } = 30d;
    }
}

public class FloatTypeInfoCarrierTest(FloatTypeInfoCarrierTest.FloatTypeInfoCarrierFixture fixture)
    : TypeTestBase<float, FloatTypeInfoCarrierTest.FloatTypeInfoCarrierFixture>(fixture)
{
    public class FloatTypeInfoCarrierFixture : TypeInfoCarrierFixture<float>
    {
        public override float Value { get; } = 30.5f;

        public override float OtherValue { get; } = 30f;
    }
}

public class DateTimeTypeInfoCarrierTest(DateTimeTypeInfoCarrierTest.DateTimeTypeInfoCarrierFixture fixture)
    : TypeTestBase<DateTime, DateTimeTypeInfoCarrierTest.DateTimeTypeInfoCarrierFixture>(fixture)
{
    public class DateTimeTypeInfoCarrierFixture : TypeInfoCarrierFixture<DateTime>
    {
        public override DateTime Value { get; } = new(2020, 1, 5, 12, 30, 45, DateTimeKind.Unspecified);

        public override DateTime OtherValue { get; } = new(2022, 5, 3, 0, 0, 0, DateTimeKind.Unspecified);
    }
}

public class DateTimeOffsetTypeInfoCarrierTest(
    DateTimeOffsetTypeInfoCarrierTest.DateTimeOffsetTypeInfoCarrierFixture fixture)
    : TypeTestBase<DateTimeOffset, DateTimeOffsetTypeInfoCarrierTest.DateTimeOffsetTypeInfoCarrierFixture>(fixture)
{
    public class DateTimeOffsetTypeInfoCarrierFixture : TypeInfoCarrierFixture<DateTimeOffset>
    {
        public override DateTimeOffset Value { get; } = new(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(2));

        public override DateTimeOffset OtherValue { get; } = new(2020, 1, 5, 12, 30, 45, TimeSpan.FromHours(3));
    }
}

public class DateOnlyTypeInfoCarrierTest(DateOnlyTypeInfoCarrierTest.DateOnlyTypeInfoCarrierFixture fixture)
    : TypeTestBase<DateOnly, DateOnlyTypeInfoCarrierTest.DateOnlyTypeInfoCarrierFixture>(fixture)
{
    public class DateOnlyTypeInfoCarrierFixture : TypeInfoCarrierFixture<DateOnly>
    {
        public override DateOnly Value { get; } = new(2020, 1, 5);

        public override DateOnly OtherValue { get; } = new(2022, 5, 3);
    }
}

public class TimeOnlyTypeInfoCarrierTest(TimeOnlyTypeInfoCarrierTest.TimeOnlyTypeInfoCarrierFixture fixture)
    : TypeTestBase<TimeOnly, TimeOnlyTypeInfoCarrierTest.TimeOnlyTypeInfoCarrierFixture>(fixture)
{
    public class TimeOnlyTypeInfoCarrierFixture : TypeInfoCarrierFixture<TimeOnly>
    {
        public override TimeOnly Value { get; } = new(12, 30, 45);

        public override TimeOnly OtherValue { get; } = new(14, 0, 0);
    }
}

public class TimeSpanTypeInfoCarrierTest(TimeSpanTypeInfoCarrierTest.TimeSpanTypeInfoCarrierFixture fixture)
    : TypeTestBase<TimeSpan, TimeSpanTypeInfoCarrierTest.TimeSpanTypeInfoCarrierFixture>(fixture)
{
    public class TimeSpanTypeInfoCarrierFixture : TypeInfoCarrierFixture<TimeSpan>
    {
        public override TimeSpan Value { get; } = new(12, 30, 45);

        public override TimeSpan OtherValue { get; } = new(14, 0, 0);
    }
}
