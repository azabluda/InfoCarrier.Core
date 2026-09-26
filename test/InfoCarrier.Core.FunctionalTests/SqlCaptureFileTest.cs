// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The one reader and the one writer of a capture's <c>.sql</c> file (#167, step H1c). Each pin
///     writes into a folder of its own and reads the bytes back.
/// </summary>
public sealed class SqlCaptureFileTest : IDisposable
{
    private static readonly SqlCaptureCommand Select = new("SELECT \"t0\".\"Id\"\nFROM \"T\" AS \"t0\"", "reads 4");
    private static readonly SqlCaptureCommand Insert = new("INSERT INTO \"T\" (\"Id\")\nVALUES (@p0)", "rows 1");
    private static readonly SqlCaptureCommand Refused = new("INSERT INTO \"T\" (\"Id\")\nVALUES (@p0)", "failed");
    private static readonly SqlCaptureCommand Count = new("SELECT COUNT(*)\nFROM \"T\" AS \"t0\"", "scalar");

    private static readonly SqlCaptureCase WhereFalse = new("Where(async: False)", 1);
    private static readonly SqlCaptureCase WhereFalseAgain = new("Where(async: False)", 2);
    private static readonly SqlCaptureCase WhereTrue = new("Where(async: True)", 1);
    private static readonly SqlCaptureCase Add = new("Add", 1);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), nameof(SqlCaptureFileTest), Guid.NewGuid().ToString("N"));

    public SqlCaptureFileTest()
        => Directory.CreateDirectory(_folder);

    public void Dispose()
        => Directory.Delete(_folder, recursive: true);

    [ConditionalFact]
    public void A_missing_file_reads_as_empty()
        => Assert.Empty(SqlCaptureFile.Read(PathOf("missing.sql")).Cases);

    [ConditionalFact]
    public void A_file_reads_back_what_it_wrote()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(WhereTrue, [Select, Insert], directRunFailed: false);
        file.Set(Add, [Refused, Count], directRunFailed: true);
        file.Write(PathOf("x.sql"));

        SqlCaptureFile read = SqlCaptureFile.Read(PathOf("x.sql"));

        Assert.Equal([Add, WhereTrue], read.Cases.OrderBy(c => c.Name, StringComparer.Ordinal));
        Assert.Equal([Select, Insert], read.Find(WhereTrue)!);
        Assert.False(read.DirectRunFailed(WhereTrue));
        Assert.Equal([Refused, Count], read.Find(Add)!);
        Assert.True(read.DirectRunFailed(Add));
    }

    [ConditionalFact]
    public void Cases_with_the_same_commands_share_one_entry_sorted_by_the_first_name()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(WhereTrue, [Select], directRunFailed: false);
        file.Set(WhereFalse, [Select], directRunFailed: false);
        file.Set(Add, [Insert], directRunFailed: false);
        file.Set(WhereFalseAgain, [Select], directRunFailed: false);

        file.Write(PathOf("x.sql"));

        Assert.Equal(
            """
            -- Add
            -- #1 rows 1
            INSERT INTO "T" ("Id")
            VALUES (@p0)

            -- Where(async: False)
            -- Where(async: False) #2
            -- Where(async: True)
            -- #1 reads 4
            SELECT "t0"."Id"
            FROM "T" AS "t0"
            """ + "\n",
            File.ReadAllText(PathOf("x.sql")));
    }

    /// <summary>A capture with <c>--filter</c> rewrites only the tests it ran (review focus 3).</summary>
    [ConditionalFact]
    public void Set_on_one_case_leaves_every_other_entry_byte_for_byte()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(WhereTrue, [Select], directRunFailed: false);
        file.Set(WhereFalse, [Select], directRunFailed: false);
        file.Set(Add, [Insert], directRunFailed: false);
        file.Write(PathOf("before.sql"));

        SqlCaptureFile.Read(PathOf("before.sql")).Write(PathOf("untouched.sql"));
        SqlCaptureFile changed = SqlCaptureFile.Read(PathOf("before.sql"));
        changed.Set(Add, [Refused], directRunFailed: false);
        changed.Write(PathOf("after.sql"));

        string before = File.ReadAllText(PathOf("before.sql"));
        string after = File.ReadAllText(PathOf("after.sql"));
        Assert.Equal(before, File.ReadAllText(PathOf("untouched.sql")));
        Assert.Equal(before.Split("\n\n")[1], after.Split("\n\n")[1]);
        Assert.NotEqual(before.Split("\n\n")[0], after.Split("\n\n")[0]);
    }

    [ConditionalFact]
    public void A_changed_case_leaves_its_group_and_the_group_splits()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(WhereTrue, [Select], directRunFailed: false);
        file.Set(WhereFalse, [Select], directRunFailed: false);

        file.Set(WhereTrue, [Select, Insert], directRunFailed: false);
        file.Write(PathOf("x.sql"));

        Assert.Equal(
            """
            -- Where(async: False)
            -- #1 reads 4
            SELECT "t0"."Id"
            FROM "T" AS "t0"

            -- Where(async: True)
            -- #1 reads 4
            SELECT "t0"."Id"
            FROM "T" AS "t0"
            -- #2 rows 1
            INSERT INTO "T" ("Id")
            VALUES (@p0)
            """ + "\n",
            File.ReadAllText(PathOf("x.sql")));
    }

    [ConditionalFact]
    public void A_case_with_no_commands_is_its_headers_alone_and_differs_from_a_missing_case()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(Add, [], directRunFailed: true);
        file.Write(PathOf("x.sql"));

        SqlCaptureFile read = SqlCaptureFile.Read(PathOf("x.sql"));

        Assert.Equal("-- Add\n-- direct run failed\n", File.ReadAllText(PathOf("x.sql")));
        Assert.Empty(read.Find(Add)!);
        Assert.True(read.DirectRunFailed(Add));
        Assert.Null(read.Find(WhereTrue));
    }

    /// <summary>A theory argument can print a line break or <c>--</c> (review focus 5).</summary>
    [ConditionalFact]
    public void A_name_with_a_line_break_and_dashes_stays_one_header_line_and_reads_back()
    {
        var odd = new SqlCaptureCase("Where(text: \"a\r\nb -- c \\ d\")", 1);
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(odd, [Select], directRunFailed: false);
        file.Write(PathOf("x.sql"));

        string[] lines = File.ReadAllText(PathOf("x.sql")).Split('\n');

        Assert.Equal("-- Where(text: \"a\\r\\nb -- c \\\\ d\")", lines[0]);
        Assert.Equal("-- #1 reads 4", lines[1]);
        Assert.Equal([odd], SqlCaptureFile.Read(PathOf("x.sql")).Cases);
    }

    /// <summary>
    ///     A <c>TagWith</c> comment is part of its command, even one that reads like a header, because
    ///     headers come only before <c>-- #1</c>.
    /// </summary>
    [ConditionalFact]
    public void A_comment_inside_a_command_is_part_of_the_command()
    {
        var tagged = new SqlCaptureCommand("-- Where(async: True)\n-- direct run failed\nSELECT 1", "reads 2");
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(Add, [tagged, Count], directRunFailed: false);
        file.Write(PathOf("x.sql"));

        SqlCaptureFile read = SqlCaptureFile.Read(PathOf("x.sql"));

        Assert.Equal([Add], read.Cases);
        Assert.Equal([tagged, Count], read.Find(Add)!);
        Assert.False(read.DirectRunFailed(Add));
    }

    /// <summary>
    ///     Text the reader would split differently is refused when written, never written and misread:
    ///     an empty line ends an entry, and <c>-- #2 </c> starts the second command.
    /// </summary>
    [ConditionalFact]
    public void Text_the_reader_would_misread_is_refused()
    {
        SqlCaptureFile file = SqlCaptureFile.Read(PathOf("missing.sql"));
        file.Set(Add, [new("SELECT 1\n\nFROM \"T\"", "reads 1")], directRunFailed: false);
        Assert.Throws<InvalidOperationException>(() => file.Write(PathOf("x.sql")));

        file.Set(Add, [new("-- #2 reads 9\nSELECT 1", "reads 1"), Count], directRunFailed: false);
        Assert.Throws<InvalidOperationException>(() => file.Write(PathOf("x.sql")));
    }

    private string PathOf(string name)
        => Path.Combine(_folder, name);
}
