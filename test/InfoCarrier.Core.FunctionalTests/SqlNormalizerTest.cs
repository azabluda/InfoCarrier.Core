// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     What <see cref="SqlNormalizer" /> treats as a name, and what it keeps (#167, step H1b). Each
///     pin is one input and its expected output, and none runs a store.
/// </summary>
public class SqlNormalizerTest
{
    [ConditionalFact]
    public void A_parameter_is_numbered_by_first_appearance_whatever_its_name()
    {
        Assert.Equal("WHERE \"x\" = @p0", SqlNormalizer.Normalize("WHERE \"x\" = @city"));
        Assert.Equal("WHERE \"x\" = @p0", SqlNormalizer.Normalize("WHERE \"x\" = @Value"));
        Assert.Equal(
            "WHERE \"x\" = @p0 OR \"y\" = @p1 OR \"z\" = @p0",
            SqlNormalizer.Normalize("WHERE \"x\" = @b OR \"y\" = @a OR \"z\" = @b"));
    }

    [ConditionalFact]
    public void A_table_alias_is_numbered_as_alias_and_as_qualifier()
        => Assert.Equal(
            """
            FROM "Customers" AS "t0" WHERE "t0"."City" = @p0
            """,
            SqlNormalizer.Normalize(
                """
                FROM "Customers" AS "c" WHERE "c"."City" = @city
                """));

    [ConditionalFact]
    public void A_table_alias_is_numbered_where_it_first_appears_even_before_its_FROM()
        => Assert.Equal(
            """
            SELECT "t0"."Id", "t1"."Id"
            FROM "Customers" AS "t0"
            INNER JOIN "Orders" AS "t1" ON "t0"."Id" = "t1"."CustomerId"
            """,
            SqlNormalizer.Normalize(
                """
                SELECT "c"."Id", "o"."Id"
                FROM "Customers" AS "c"
                INNER JOIN "Orders" AS "o" ON "c"."Id" = "o"."CustomerId"
                """));

    /// <summary>EF writes no <c>AS</c> where the alias equals the column's own name.</summary>
    [ConditionalFact]
    public void A_column_alias_goes()
        => Assert.Equal(
            SqlNormalizer.Normalize(
                """
                SELECT "c"."Id"
                FROM "Customers" AS "c"
                """),
            SqlNormalizer.Normalize(
                """
                SELECT "c"."Id" AS "Item1"
                FROM "Customers" AS "c"
                """));

    [ConditionalFact]
    public void A_column_of_a_derived_table_is_numbered_within_its_alias()
    {
        string titled = SqlNormalizer.Normalize(
            """
            SELECT "s"."Title"
            FROM (
                SELECT "c"."Name" AS "Title"
                FROM "Customers" AS "c"
            ) AS "s"
            """);
        string positional = SqlNormalizer.Normalize(
            """
            SELECT "s"."Item1"
            FROM (
                SELECT "c"."Name" AS "Item1"
                FROM "Customers" AS "c"
            ) AS "s"
            """);

        Assert.Equal(titled, positional);
        Assert.Equal(
            """
            SELECT "t0"."c0"
            FROM (
                SELECT "t1"."Name"
                FROM "Customers" AS "t1"
            ) AS "t0"
            """,
            titled);
    }

    [ConditionalFact]
    public void A_column_of_a_function_is_numbered_within_its_alias()
        => Assert.Equal(
            """
            SELECT "t0"."Id", "t1"."c0"
            FROM "Tickets" AS "t0"
            JOIN json_each("t0"."Markers") AS "t1"
            ORDER BY "t1"."c1", "t1"."c0"
            """,
            SqlNormalizer.Normalize(
                """
                SELECT "t"."Id", "m"."value"
                FROM "Tickets" AS "t"
                JOIN json_each("t"."Markers") AS "m"
                ORDER BY "m"."key", "m"."value"
                """));

    [ConditionalFact]
    public void A_column_of_a_base_table_keeps_its_name()
        => Assert.NotEqual(
            SqlNormalizer.Normalize("""SELECT "c"."City" FROM "Customers" AS "c" """),
            SqlNormalizer.Normalize("""SELECT "c"."Region" FROM "Customers" AS "c" """));

    [ConditionalFact]
    public void A_literal_and_a_parameter_still_differ()
        => Assert.NotEqual(
            SqlNormalizer.Normalize("""WHERE "c"."City" = 'London'"""),
            SqlNormalizer.Normalize("""WHERE "c"."City" = @city"""));

    [ConditionalFact]
    public void A_string_literal_is_kept_whole()
        => Assert.Equal(
            """SELECT 'a "x" AS "y" @z', 'it''s' FROM "T" AS "t0" """.TrimEnd(),
            SqlNormalizer.Normalize("""SELECT 'a "x" AS "y" @z', 'it''s' FROM "T" AS "t" """));

    [ConditionalFact]
    public void A_line_comment_is_kept_whole()
        => Assert.Equal(
            """
            -- AS "q" @w "c".
            SELECT "t0"."Id"
            FROM "T" AS "t0"
            """,
            SqlNormalizer.Normalize(
                """
                -- AS "q" @w "c".
                SELECT "c"."Id"
                FROM "T" AS "c"
                """));

    [ConditionalFact]
    public void A_scalar_subquery_in_a_select_list_loses_its_column_alias_and_keeps_its_table_alias()
        => Assert.Equal(
            """
            SELECT (SELECT COUNT(*) FROM "Orders" AS "t0")
            FROM "Customers" AS "t1"
            """,
            SqlNormalizer.Normalize(
                """
                SELECT (SELECT COUNT(*) FROM "Orders" AS "o") AS "n"
                FROM "Customers" AS "c"
                """));

    [ConditionalFact]
    public void CAST_keeps_its_AS()
        => Assert.Equal(
            """SELECT CAST("x"."X" AS TEXT)""",
            SqlNormalizer.Normalize("""SELECT CAST("x"."X" AS TEXT)"""));

    [ConditionalFact]
    public void UPDATE_names_a_table_alias()
        => Assert.Equal(
            """
            UPDATE "Customers" AS "t0"
            SET "Name" = @p0
            WHERE "t0"."Id" = @p1
            """,
            SqlNormalizer.Normalize(
                """
                UPDATE "Customers" AS "c"
                SET "Name" = @name
                WHERE "c"."Id" = @id
                """));

    [ConditionalFact]
    public void A_lateral_join_names_a_derived_table()
        => Assert.Equal(
            """
            SELECT "t0"."Id", "t1"."c0"
            FROM "Customers" AS "t0"
            LEFT JOIN LATERAL (
                SELECT "t2"."Id"
                FROM "Orders" AS "t2"
            ) AS "t1" ON TRUE
            """,
            SqlNormalizer.Normalize(
                """
                SELECT "c"."Id", "s"."Id"
                FROM "Customers" AS "c"
                LEFT JOIN LATERAL (
                    SELECT "o"."Id"
                    FROM "Orders" AS "o"
                ) AS "s" ON TRUE
                """));

    [ConditionalFact]
    public void A_batch_numbers_its_parameters_across_the_whole_command()
        => Assert.Equal(
            """
            INSERT INTO "T" ("A", "B")
            VALUES (@p0, @p1);
            SELECT "t0"."Id"
            FROM "T" AS "t0"
            WHERE "t0"."A" = @p2
            """,
            SqlNormalizer.Normalize(
                """
                INSERT INTO "T" ("A", "B")
                VALUES (@a0, @b0);
                SELECT "t"."Id"
                FROM "T" AS "t"
                WHERE "t"."A" = @a1
                """));

    /// <summary>The file ends an entry at an empty line, so a statement never holds one.</summary>
    [ConditionalFact]
    public void Line_ends_are_LF_with_no_trailing_space_and_no_empty_line()
        => Assert.Equal(
            "SELECT 1\n  FROM \"T\" AS \"t0\"",
            SqlNormalizer.Normalize("SELECT 1   \r\n\r\n  FROM \"T\" AS \"t\"\r\n"));
}
