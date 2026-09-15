// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.Query.Translations.Operators;
using Microsoft.EntityFrameworkCore.Query.Translations.Temporal;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     EF Core 10's per-type translation suites on ADR-009 <b>Tier B</b> — all sixteen of them.
/// </summary>
/// <remarks>
///     <para>
///         These replaced the single sprawling <c>*FunctionsQuery</c> base with one class per CLR
///         type or operator family, over one shared model (<c>BasicTypesQueryFixtureBase</c>). They
///         are the densest scalar coverage EF has: every value here crosses the wire as a constant,
///         a parameter or a projected column, which is exactly what <c>PrimitiveCoercion</c> and
///         the allowlist decide (A19, A34).
///     </para>
///     <para>
///         <b>Tier B since R57</b>, and A81's rule is why: when a base could run on either tier,
///         the tier that <em>translates</em> is the one whose green means more. R43 had priced the
///         move at 217 overrides — the size of EF's SQLite <c>Translations</c> suite — and left it
///         for the owner. The measured cost is <b>65</b>, because most of EF's 217 exist only to
///         assert golden SQL over a base call that already passes, and a provider only writes an
///         override for a test that actually fails.
///     </para>
///     <para>
///         Every one of those 65 is the store rather than this provider: each failed with
///         <c>The LINQ expression … could not be translated</c>, naming a member SQLite has no
///         function for, and EF's own SQLite class answers each with the same
///         <c>AssertTranslationFailed</c>. The move also <em>deleted</em> nine overrides — see the
///         remarks on <see cref="StringTranslationsInfoCarrierTest" /> and
///         <see cref="MiscellaneousTranslationsInfoCarrierTest" />.
///     </para>
/// </remarks>
public class ByteArrayTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ByteArrayTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own ByteArrayTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 16428,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/ByteArrayTranslationsSqliteTest.cs", 32, 33,
        Justification = "Array access. Issue #16428.")]
    public override Task First()
        => AssertTranslationFailed(() => base.First());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 16428,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/ByteArrayTranslationsSqliteTest.cs", 28, 29,
        Justification = "Array access. Issue #16428.")]
    public override Task Index()
        => AssertTranslationFailed(() => base.Index());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class EnumTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : EnumTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class GuidTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : GuidTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own GuidTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/GuidTranslationsSqliteTest.cs", 52, 53,
        Justification = Upstream.GaveNoReason)]
    public override Task NewGuid()
        => AssertTranslationFailed(() => base.NewGuid());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class MathTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MathTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own MathTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MathTranslationsSqliteTest.cs", 15, 16,
        Justification = "SQLite decimal support")]
    public override Task Abs_decimal()
        => AssertTranslationFailed(() => base.Abs_decimal());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MathTranslationsSqliteTest.cs", 78, 79,
        Justification = "SQLite decimal support")]
    public override Task Floor_decimal()
        => AssertTranslationFailed(() => base.Floor_decimal());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MathTranslationsSqliteTest.cs", 129, 130,
        Justification = "SQLite decimal support")]
    public override Task Round_decimal()
        => AssertTranslationFailed(() => base.Round_decimal());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MathTranslationsSqliteTest.cs", 166, 167,
        Justification = "SQLite decimal support")]
    public override Task Round_with_digits_decimal()
        => AssertTranslationFailed(() => base.Round_with_digits_decimal());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MathTranslationsSqliteTest.cs", 193, 194,
        Justification = "SQLite decimal support")]
    public override Task Truncate_decimal()
        => AssertTranslationFailed(() => base.Truncate_decimal());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
/// <remarks>
///     <b>R57 deleted six overrides here by inheriting the base they were copied from.</b> A27
///     had transcribed <c>MiscellaneousTranslationsRelationalTestBase</c>'s six <c>Random.Next</c>
///     expectations by hand, because that base was out of reach; the store move puts it in reach.
///     The reasoning behind them is unchanged and still worth keeping: <c>Random.Next()</c> in a
///     <c>Where</c> is client code in a row-deciding argument, so <c>RejectClientEvaluation</c>
///     refuses it (A44) — and it is worse here than for a relational provider, since a random
///     number drawn on the client would decide which rows are fetched, once, and then be gone.
/// </remarks>
public class MiscellaneousTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MiscellaneousTranslationsRelationalTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own MiscellaneousTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 75, 76,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToBoolean()
        => AssertTranslationFailed(() => base.Convert_ToBoolean());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 78, 79,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToByte()
        => AssertTranslationFailed(() => base.Convert_ToByte());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 81, 82,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToDecimal()
        => AssertTranslationFailed(() => base.Convert_ToDecimal());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 84, 85,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToDouble()
        => AssertTranslationFailed(() => base.Convert_ToDouble());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 87, 88,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToInt16()
        => AssertTranslationFailed(() => base.Convert_ToInt16());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 90, 91,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToInt32()
        => AssertTranslationFailed(() => base.Convert_ToInt32());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 93, 94,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToInt64()
        => AssertTranslationFailed(() => base.Convert_ToInt64());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/MiscellaneousTranslationsSqliteTest.cs", 96, 97,
        Justification = Upstream.GaveNoReason)]
    public override Task Convert_ToString()
        => AssertTranslationFailed(() => base.Convert_ToString());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
/// <remarks>
///     <b>R57 deleted three overrides here because the store move makes them false.</b> They were
///     EF's own <c>StringTranslationsInMemoryTest</c>: the base asserts that
///     <c>StringComparison.CurrentCulture</c>/<c>InvariantCulture</c> are unsupported, and the
///     InMemory store supported them, so the assertion could not hold. SQLite does not support
///     them, so the base's own expectation is now the right one. This is the tier rule paying
///     out — a workaround deleted by moving the class rather than by writing around it.
/// </remarks>
public class StringTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : StringTranslationsRelationalTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own StringTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/StringTranslationsSqliteTest.cs", 1411, 1412,
        Justification = Upstream.GaveNoReason)]
    public override Task Join_non_aggregate()
        => AssertTranslationFailed(() => base.Join_non_aggregate());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class ArithmeticOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ArithmeticOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class BitwiseOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : BitwiseOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own BitwiseOperatorTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Operators/BitwiseOperatorTranslationsSqliteTest.cs", 175, 176,
        Justification = Upstream.GaveNoReason)]
    public override Task Left_shift()
        => AssertTranslationFailed(() => base.Left_shift());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Operators/BitwiseOperatorTranslationsSqliteTest.cs", 178, 179,
        Justification = Upstream.GaveNoReason)]
    public override Task Right_shift()
        => AssertTranslationFailed(() => base.Right_shift());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 16645,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Operators/BitwiseOperatorTranslationsSqliteTest.cs", 95, 97,
        Justification = "Issue #16645 bitwise xor support",
        Deviation = DeviationKind.UpstreamAssertsNothing)]
    public override Task Xor()
        => AssertTranslationFailed(() => base.Xor());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 16645,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Operators/BitwiseOperatorTranslationsSqliteTest.cs", 99, 101,
        Justification = "Issue #16645 bitwise xor support",
        Deviation = DeviationKind.UpstreamAssertsNothing)]
    public override Task Xor_over_boolean()
        => AssertTranslationFailed(() => base.Xor_over_boolean());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class ComparisonOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : ComparisonOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class LogicalOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : LogicalOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class MiscellaneousOperatorTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : MiscellaneousOperatorTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture);

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateOnlyTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateOnlyTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own DateOnlyTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsSqliteTest.cs", 217, 222,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task ToDateTime_constant_DateTime_with_property_TimeOnly()
        => AssertTranslationFailed(() => base.ToDateTime_constant_DateTime_with_property_TimeOnly());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsSqliteTest.cs", 203, 208,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task ToDateTime_property_with_constant_TimeOnly()
        => AssertTranslationFailed(() => base.ToDateTime_property_with_constant_TimeOnly());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsSqliteTest.cs", 210, 215,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task ToDateTime_property_with_property_TimeOnly()
        => AssertTranslationFailed(() => base.ToDateTime_property_with_property_TimeOnly());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsSqliteTest.cs", 224, 229,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task ToDateTime_with_complex_DateTime()
        => AssertTranslationFailed(() => base.ToDateTime_with_complex_DateTime());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateOnlyTranslationsSqliteTest.cs", 231, 236,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task ToDateTime_with_complex_TimeOnly()
        => AssertTranslationFailed(() => base.ToDateTime_with_complex_TimeOnly());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateTimeTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateTimeTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own DateTimeTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeTranslationsSqliteTest.cs", 189, 190,
        Justification = Upstream.GaveNoReason)]
    public override Task subtract_and_TotalDays()
        => AssertTranslationFailed(() => base.subtract_and_TotalDays());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class DateTimeOffsetTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : DateTimeOffsetTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own DateTimeOffsetTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 29, 34,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Date()
        => AssertTranslationFailed(() => base.Date());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 57, 62,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Day()
        => AssertTranslationFailed(() => base.Day());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 50, 55,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task DayOfYear()
        => AssertTranslationFailed(() => base.DayOfYear());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 64, 69,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Hour()
        => AssertTranslationFailed(() => base.Hour());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 92, 97,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Microsecond()
        => AssertTranslationFailed(() => base.Microsecond());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 85, 90,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Millisecond()
        => AssertTranslationFailed(() => base.Millisecond());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 71, 76,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Minute()
        => AssertTranslationFailed(() => base.Minute());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 43, 48,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Month()
        => AssertTranslationFailed(() => base.Month());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 99, 104,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Nanosecond()
        => AssertTranslationFailed(() => base.Nanosecond());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 15, 20,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Now()
        => AssertTranslationFailed(() => base.Now());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 78, 83,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Second()
        => AssertTranslationFailed(() => base.Second());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 194, 195,
        Justification = Upstream.GaveNoReason)]
    public override Task ToUnixTimeMilliseconds()
        => AssertTranslationFailed(() => base.ToUnixTimeMilliseconds());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 197, 198,
        Justification = Upstream.GaveNoReason)]
    public override Task ToUnixTimeSecond()
        => AssertTranslationFailed(() => base.ToUnixTimeSecond());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 22, 27,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task UtcNow()
        => AssertTranslationFailed(() => base.UtcNow());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/DateTimeOffsetTranslationsSqliteTest.cs", 36, 41,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Year()
        => AssertTranslationFailed(() => base.Year());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class TimeOnlyTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : TimeOnlyTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own TimeOnlyTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 63, 69,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task AddHours()
        => AssertTranslationFailed(() => base.AddHours());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 71, 77,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task AddMinutes()
        => AssertTranslationFailed(() => base.AddMinutes());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 79, 85,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Add_TimeSpan()
        => AssertTranslationFailed(() => base.Add_TimeSpan());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 119, 125,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task FromDateTime_compared_to_constant()
        => AssertTranslationFailed(() => base.FromDateTime_compared_to_constant());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 111, 117,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task FromDateTime_compared_to_parameter()
        => AssertTranslationFailed(() => base.FromDateTime_compared_to_parameter());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 103, 109,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task FromDateTime_compared_to_property()
        => AssertTranslationFailed(() => base.FromDateTime_compared_to_property());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 135, 141,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task FromTimeSpan_compared_to_parameter()
        => AssertTranslationFailed(() => base.FromTimeSpan_compared_to_parameter());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 127, 133,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task FromTimeSpan_compared_to_property()
        => AssertTranslationFailed(() => base.FromTimeSpan_compared_to_property());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 15, 21,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Hour()
        => AssertTranslationFailed(() => base.Hour());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 87, 93,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task IsBetween()
        => AssertTranslationFailed(() => base.IsBetween());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 47, 53,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Microsecond()
        => AssertTranslationFailed(() => base.Microsecond());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 39, 45,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Millisecond()
        => AssertTranslationFailed(() => base.Millisecond());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 23, 29,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Minute()
        => AssertTranslationFailed(() => base.Minute());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 55, 61,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Nanosecond()
        => AssertTranslationFailed(() => base.Nanosecond());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 25103,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 143, 149,
        Justification = "TimeOnly/DateOnly is not supported. Issue #25103.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Order_by_FromTimeSpan()
        => AssertTranslationFailed(() => base.Order_by_FromTimeSpan());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 31, 37,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Second()
        => AssertTranslationFailed(() => base.Second());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeOnlyTranslationsSqliteTest.cs", 95, 101,
        Justification = "TimeSpan. Issue #18844.",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Subtract()
        => AssertTranslationFailed(() => base.Subtract());
}

/// <inheritdoc cref="ByteArrayTranslationsInfoCarrierTest" />
public class TimeSpanTranslationsInfoCarrierTest(BasicTypesQueryInfoCarrierFixture fixture)
    : TimeSpanTranslationsTestBase<BasicTypesQueryInfoCarrierFixture>(fixture)
{
    // --- SQLite cannot translate these, and EF's own TimeSpanTranslationsSqliteTest says so
    // with the same `AssertTranslationFailed`. Adopted one by one from the tests that
    // actually failed rather than copied wholesale: EF overrides every test in this
    // class, most of them only to assert golden SQL over a base call that passes.

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 16, 21,
        Justification = "Translate TimeSpan members, #18844",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Hours()
        => AssertTranslationFailed(() => base.Hours());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 47, 52,
        Justification = "Translate TimeSpan members, #18844",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Microseconds()
        => AssertTranslationFailed(() => base.Microseconds());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 39, 44,
        Justification = "Translate TimeSpan members, #18844",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Milliseconds()
        => AssertTranslationFailed(() => base.Milliseconds());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 24, 29,
        Justification = "Translate TimeSpan members, #18844",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Minutes()
        => AssertTranslationFailed(() => base.Minutes());

    /// <inheritdoc />
    [StoreIssue(
        IssueTracker.EfCore, 18844,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 55, 60,
        Justification = "Translate TimeSpan members, #18844",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Nanoseconds()
        => AssertTranslationFailed(() => base.Nanoseconds());

    /// <inheritdoc />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/Translations/Temporal/TimeSpanTranslationsSqliteTest.cs", 31, 36,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.SqlNotAsserted)]
    public override Task Seconds()
        => AssertTranslationFailed(() => base.Seconds());
}

/// <summary>
///     The basic-types fixture, wired to an InMemory backend behind the wire. Shared by all
///     sixteen classes above, exactly as EF shares its own.
/// </summary>
public class BasicTypesQueryInfoCarrierFixture : BasicTypesQueryFixtureBase, ITestSqlLoggerFactory
{
    /// <summary>
    ///     The compliance gate's second assertion (R54). The property is real —
    ///     <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> returns a
    ///     <c>TestSqlLoggerFactory</c> — but what it observes is the <em>client's</em> log, and
    ///     this client has no database and emits no SQL. <c>ServerSqlLog</c> is where the
    ///     server's statements can actually be read.
    /// </summary>
    public TestSqlLoggerFactory TestSqlLoggerFactory
        => (TestSqlLoggerFactory)ListLoggerFactory;

    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
}
