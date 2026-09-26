// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.InMemory;
using InfoCarrier.Core.FunctionalTests.Sqlite.Query;
using InfoCarrier.Core.FunctionalTests.Sqlite.Update;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     Where a class's capture lives, what a normal run compares, and how two cases that print alike
///     are told apart (#167, step H1d).
/// </summary>
public class SqlCaptureAssertionTest
{
    private static readonly string Project = Path.Combine("test", "InfoCarrier.Core.FunctionalTests");

    [ConditionalFact]
    public void A_class_captures_beside_its_source_file()
        => Assert.EndsWith(
            Path.Combine(Project, "Sqlite", "Query", "NorthwindWhereQuerySqliteInfoCarrierTest.wire.sql"),
            SqlCapture.FileFor(typeof(NorthwindWhereQuerySqliteInfoCarrierTest), SqlCaptureMode.Wire),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void The_direct_side_is_the_same_name_with_direct()
        => Assert.EndsWith(
            Path.Combine(Project, "Sqlite", "Query", "NorthwindWhereQuerySqliteInfoCarrierTest.direct.sql"),
            SqlCapture.FileFor(typeof(NorthwindWhereQuerySqliteInfoCarrierTest), SqlCaptureMode.Direct),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_nested_class_keeps_its_outer_class_in_the_name()
        => Assert.EndsWith(
            Path.Combine(Project, "Sqlite", "Update", "ProxyGraphUpdatesInfoCarrierTest.LazyLoading.wire.sql"),
            SqlCapture.FileFor(typeof(ProxyGraphUpdatesInfoCarrierTest.LazyLoading), SqlCaptureMode.Wire),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_class_of_Tier_A_has_no_file()
        => Assert.Null(SqlCapture.FileFor(typeof(InMemorySmokeTest), SqlCaptureMode.Wire));

    [ConditionalFact]
    public void The_mode_comes_from_one_variable_and_refuses_a_value_it_does_not_know()
    {
        Assert.Equal(SqlCaptureMode.Assert, SqlCapture.ParseMode(null));
        Assert.Equal(SqlCaptureMode.Assert, SqlCapture.ParseMode(string.Empty));
        Assert.Equal(SqlCaptureMode.Wire, SqlCapture.ParseMode("wire"));
        Assert.Equal(SqlCaptureMode.Direct, SqlCapture.ParseMode("direct"));
        Assert.Contains("INFOCARRIER_SQL_CAPTURE", Assert.Throws<InvalidOperationException>(() => SqlCapture.ParseMode("Wire")).Message, StringComparison.Ordinal);
    }

    /// <summary>A count is recorded and never asserted (decision 8).</summary>
    [ConditionalFact]
    public void Equal_text_with_different_counts_is_equal()
        => Assert.Null(SqlCapture.Compare(
            [new SqlCaptureCommand("SELECT \"t0\".\"Id\"\nFROM \"T\" AS \"t0\"", "reads 7")],
            [Ran("SELECT \"t\".\"Id\"\r\nFROM \"T\" AS \"t\"", count: 3)],
            "SomeTest"));

    [ConditionalFact]
    public void A_changed_statement_is_named_with_the_capture_command()
    {
        string? message = SqlCapture.Compare(
            [new SqlCaptureCommand("SELECT 1", "reads 2"), new SqlCaptureCommand("SELECT 2", "reads 2")],
            [Ran("SELECT 1"), Ran("SELECT 3")],
            "SomeTest");

        Assert.NotNull(message);
        Assert.Contains("command #2 differs", message, StringComparison.Ordinal);
        Assert.Contains("SELECT 3", message, StringComparison.Ordinal);
        Assert.Contains("eng/sql-capture.sh --filter SomeTest", message, StringComparison.Ordinal);
    }

    [ConditionalFact]
    public void An_extra_statement_is_named()
        => Assert.Contains(
            "command #2 is extra",
            SqlCapture.Compare([new SqlCaptureCommand("SELECT 1", "reads 2")], [Ran("SELECT 1"), Ran("SELECT 2")], "SomeTest"),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_missing_statement_is_named()
        => Assert.Contains(
            "command #2 is missing",
            SqlCapture.Compare(
                [new SqlCaptureCommand("SELECT 1", "reads 2"), new SqlCaptureCommand("SELECT 2", "reads 2")],
                [Ran("SELECT 1")],
                "SomeTest"),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_failure_mark_is_compared()
        => Assert.Contains(
            "command #1 differs",
            SqlCapture.Compare([new SqlCaptureCommand("INSERT 1", "rows 1")], [Ran("INSERT 1", failed: true)], "SomeTest"),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_missing_entry_is_named()
        => Assert.Contains(
            "has no entry for this test",
            SqlCapture.Compare(null, [], "SomeTest"),
            StringComparison.Ordinal);

    /// <summary>
    ///     Two rows that xUnit prints alike are two entries, numbered in the order they run (review
    ///     focus 1).
    /// </summary>
    [ConditionalTheory]
    [MemberData(nameof(Alike))]
    public void Rows_that_print_alike_get_ordinals_in_the_order_they_run(Same row)
    {
        Assert.Equal(row.Number, CurrentTest.Value!.Ordinal);
        Assert.Equal(SqlCapture.CaseOf(CurrentTest.Value!), new SqlCaptureCase($"{nameof(Rows_that_print_alike_get_ordinals_in_the_order_they_run)}(row: same)", row.Number));
    }

    /// <summary>Two rows of a type xUnit cannot serialize, with one <c>ToString()</c>.</summary>
    public static IEnumerable<object[]> Alike
        => [[new Same(1)], [new Same(2)]];

    private static CapturedCommand Ran(string text, int? count = 2, bool failed = false)
        => new(text, CapturedCommandKind.Reader, count, failed);

    /// <summary>A theory argument that xUnit cannot serialize, and whose rows print alike.</summary>
    public sealed class Same(int number)
    {
        public int Number { get; } = number;

        /// <inheritdoc />
        public override string ToString()
            => "same";
    }
}
