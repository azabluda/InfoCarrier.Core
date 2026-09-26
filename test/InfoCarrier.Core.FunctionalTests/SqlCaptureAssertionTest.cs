// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.InMemory;
using InfoCarrier.Core.FunctionalTests.Sqlite.Query;
using InfoCarrier.Core.FunctionalTests.Sqlite.Update;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;
using Xunit.Sdk;

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

    /// <summary>
    ///     A capture reads its file in <c>After</c>, where xUnit turns an exception into the test's
    ///     failure. Anywhere later it would escape every runner and abort the run (review, 2026-09-26).
    /// </summary>
    [ConditionalFact]
    public void A_capture_that_cannot_read_its_file_fails_the_test_and_not_the_run()
    {
        using var folder = new ScratchFolder();
        string path = folder.PathOf("Some.wire.sql");
        File.WriteAllText(path, "not a capture\n");

        XunitException failure = Assert.Throws<XunitException>(
            () => new SqlCaptureRun(SqlCaptureMode.Wire).Prepare(typeof(SqlCaptureAssertionTest), Case, [], path));

        Assert.Contains(path, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>A command the file cannot hold fails its test in <c>After</c>, before anything is written.</summary>
    [ConditionalFact]
    public void A_command_the_file_cannot_hold_fails_the_test_and_not_the_run()
    {
        using var folder = new ScratchFolder();

        XunitException failure = Assert.Throws<XunitException>(
            () => new SqlCaptureRun(SqlCaptureMode.Wire).Prepare(
                typeof(SqlCaptureAssertionTest),
                Case,
                [Ran("-- #2 reads 9\nSELECT 1")],
                folder.PathOf("Some.wire.sql")));

        Assert.Contains("'-- #2 '", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     The whole flow of a capture: the entry is prepared in <c>After</c>, a failed test marks a
    ///     direct run, and the class's end writes the file.
    /// </summary>
    [ConditionalFact]
    public void A_direct_capture_writes_the_class_when_it_ends_and_marks_a_failed_run()
    {
        using var folder = new ScratchFolder();
        string path = folder.PathOf("Some.direct.sql");
        var run = new SqlCaptureRun(SqlCaptureMode.Direct);

        run.Prepare(typeof(SqlCaptureAssertionTest), Case, [Ran("SELECT \"t\".\"Id\"\r\nFROM \"T\" AS \"t\"", count: 4)], path);
        run.TestFinished(typeof(SqlCaptureAssertionTest), Case, failed: true);

        Assert.Null(run.ClassFinished(typeof(SqlCaptureAssertionTest)));
        Assert.Equal("-- Some(async: True)\n-- direct run failed\n-- #1 reads 4\nSELECT \"t0\".\"Id\"\nFROM \"T\" AS \"t0\"\n", File.ReadAllText(path));
    }

    /// <summary>A write that fails is reported to the caller, which then goes on to the next class.</summary>
    [ConditionalFact]
    public void A_write_that_fails_is_reported_and_does_not_throw()
    {
        using var folder = new ScratchFolder();
        string path = folder.PathOf("Some.wire.sql");
        Directory.CreateDirectory(path);
        var run = new SqlCaptureRun(SqlCaptureMode.Wire);
        run.Prepare(typeof(SqlCaptureAssertionTest), Case, [], path);

        Assert.Contains(path, run.ClassFinished(typeof(SqlCaptureAssertionTest)), StringComparison.Ordinal);
    }

    /// <summary>A test whose <c>After</c> never ran has no entry to mark, and its end writes nothing.</summary>
    [ConditionalFact]
    public void A_test_that_ends_without_After_leaves_nothing_to_write()
    {
        var run = new SqlCaptureRun(SqlCaptureMode.Direct);

        run.TestFinished(typeof(SqlCaptureAssertionTest), Case, failed: true);

        Assert.Null(run.ClassFinished(typeof(SqlCaptureAssertionTest)));
    }

    private static SqlCaptureCase Case { get; } = new("Some(async: True)", 1);

    private static CapturedCommand Ran(string text, int? count = 2, bool failed = false)
        => new(text, CapturedCommandKind.Reader, count, failed);

    /// <summary>A folder of this test's own, deleted when the test ends.</summary>
    private sealed class ScratchFolder : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), nameof(SqlCaptureAssertionTest), Guid.NewGuid().ToString("N"));

        public ScratchFolder()
            => Directory.CreateDirectory(_path);

        public string PathOf(string name)
            => Path.Combine(_path, name);

        public void Dispose()
            => Directory.Delete(_path, recursive: true);
    }

    /// <summary>A theory argument that xUnit cannot serialize, and whose rows print alike.</summary>
    public sealed class Same(int number)
    {
        public int Number { get; } = number;

        /// <inheritdoc />
        public override string ToString()
            => "same";
    }
}
