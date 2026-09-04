// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Globalization;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>UdfDbFunctionTestBase</c> on ADR-009 <b>Tier B</b> — about thirty <c>HasDbFunction</c>
///     mappings, several with a <c>HasTranslation</c> building relational <c>SqlExpression</c>
///     nodes, queried through scalar functions, table-valued functions and views.
/// </summary>
/// <remarks>
///     <para>
///         <b>The fixture must save, and EF's base deliberately does not.</b>
///         <c>UdfFixtureBase.SeedAsync</c> ends with four <c>AddRange</c> calls and never persists
///         them; every provider fixture is expected to override it, add its own SQL functions and
///         call <c>SaveChanges</c>, as EF's SqlServer fixture does. <b>Omitting that is not a
///         quiet failure — it is an empty store</b>, and it cost this repository a whole wrong
///         classification: an earlier probe without it reported 11 "wrong answers and empty
///         results" that were nothing but a store with no rows in it.
///     </para>
///     <para>
///         <b>The scalar functions are defined per connection, because SQLite has no
///         <c>create function</c>.</b> EF's SqlServer fixture writes its definitions into the
///         database in <c>SeedAsync</c>; <c>Microsoft.Data.Sqlite</c> attaches a delegate to one
///         open <see cref="SqliteConnection" /> instead, and nothing is written to the file. So
///         they are declared here and installed by <see cref="SqliteFunctionInterceptor" /> on
///         every connection the <em>server</em> opens.
///     </para>
///     <para>
///         <b>R74 priced this at "two store-side reds and none of the other 73" and declined it;
///         that pricing was stale by R85.</b> What changed in between is R84, which made
///         <c>HasDbFunction</c> work: the reds that used to read "the client refuses the mapped
///         call" now reach the store and read <c>no such function</c>. Fourteen of the 185 named a
///         missing scalar function in <c>artifacts/measure/r85.log</c>, which is what this step
///         was priced against.
///     </para>
///     <para>
///         <b>Two things here are still the store's and are not worked around.</b> The
///         table-valued functions cannot be expressed at all — <c>Microsoft.Data.Sqlite</c>
///         registers scalar functions and SQLite has no table-valued equivalent to
///         <c>GetTopTwoSellingProducts</c>. And <c>IdentityString</c> is mapped
///         <c>[DbFunction(Schema = "dbo")]</c>, so the server emits a schema-qualified call that
///         SQLite answers with <c>near "(": syntax error</c>; a schema is not something a
///         connection-scoped function can carry.
///     </para>
///     <para>
///         <b>All 55 remaining reds, classified — 2026-09-02, out of
///         <c>artifacts/measure/r89b.log</c>.</b> 50 pass, 1 is skipped by EF itself. <b>Not one
///         is a wrong answer.</b>
///     </para>
///     <list type="table">
///         <item><description><b>29 — EF's own <c>TranslationFailed</c>.</b> The safe answer, and the one every other provider gives. Two families: a <em>queryable</em> function (<c>QF_*</c>, the table-valued ones) and a scalar function mapped as an <em>instance</em> method, whose call carries the client's own <c>DbContext</c> (R89).</description></item>
///         <item><description><b>10 — the client evaluated the mapped function</b> inside an anonymous-type projection, reaching EF's stub body. See below; this is the one family that is not simply "the store cannot".</description></item>
///         <item><description><b>6 — a <c>QF_*</c> message assertion</b>: the right refusal, different words.</description></item>
///         <item><description><b>4 — "no part of the query can be executed on the server"</b>, all <c>QF_*</c>. A refusal too, spelled in this provider's words rather than EF's.</description></item>
///         <item><description><b>2 — the store's:</b> <c>no such table: GetTopTwoSellingProducts</c>.</description></item>
///         <item><description><b>2 — the store's:</b> the schema-qualified <c>IdentityString</c> above.</description></item>
///         <item><description>1 client-side navigation read, 1 differing exception.</description></item>
///     </list>
///     <para>
///         <b>The <c>QF_*</c> family is not a lever and that was measured, not assumed.</b> Every
///         one of them is a <em>table-valued</em> function, and SQLite has none — there is no
///         <c>Microsoft.Data.Sqlite</c> registration for one, as there is for a scalar. Moving the
///         boundary so these ship would only move the failure: the two that already reach the store
///         are the proof, and they say <c>no such table</c>. Nothing here is this provider's to fix
///         and nothing here is work.
///     </para>
///     <para>
///         <b>The 10 are a real semantic gap, and a small one.</b> A mapped function in a
///         <em>final projection</em> is answered by the client's own method rather than by the
///         store, because the projection split reassembles client-typed projections here — and a
///         final projection is exactly where EF permits client evaluation, so this provider is
///         inside EF's contract rather than outside it. What differs is <em>whose</em>
///         implementation runs, which matters only for a function whose CLR body and store
///         definition disagree. In this base every stub throws, so the gap surfaces as
///         <c>NotImplementedException</c> and never as a wrong value. Closing it would mean
///         hoisting a mapped call out of the residual and into the server's tuple; not priced.
///     </para>
/// </remarks>
public class UdfDbFunctionInfoCarrierTest(UdfDbFunctionInfoCarrierTest.UdfDbFunctionInfoCarrierFixture fixture)
    : UdfDbFunctionTestBase<UdfDbFunctionInfoCarrierTest.UdfDbFunctionInfoCarrierFixture>(fixture)
{
    public class UdfDbFunctionInfoCarrierFixture : UdfFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "UDFDbFunctionInfoCarrierTests";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: builder => builder.AddInterceptors(new SqliteFunctionInterceptor(DefineFunctions)),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     <c>UdfFixtureBase.SeedAsync</c> only <em>stages</em> its entities — its last four
        ///     statements are <c>AddRange</c> calls and it never saves. EF's SqlServer fixture
        ///     finishes the job with its <c>create function</c> statements and a
        ///     <c>SaveChanges</c>; here the functions are the interceptor's, so the save is the
        ///     whole of it. Without this the store is empty and all 106 tests read zero rows.
        /// </remarks>
        protected override async Task SeedAsync(DbContext context)
        {
            await base.SeedAsync(context);

            await context.SaveChangesAsync();
        }

        /// <summary>
        ///     Defines this fixture's scalar functions on one open connection, with the semantics
        ///     of the <c>create function</c> statements EF's SqlServer fixture runs.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         <b>Only the functions this suite's failures named, and not the rest of EF's
        ///         SqlServer list.</b> Five more were written and measured first —
        ///         <c>StringLength</c>, the three <c>IdentityString</c> variants and
        ///         <c>AddValues</c> — and every one of them bought nothing, because no red here
        ///         reaches them. <c>DollarValue</c> is the one kept without a red of its own: it
        ///         is <c>StarValue</c>'s twin and shares its implementation.
        ///     </para>
        ///     <para>
        ///         The two that read the database do so on the <em>same</em> connection the
        ///         function was called on. SQLite permits a function callback to read through its
        ///         own connection — what it forbids is writing to it, or redefining functions on
        ///         it — and a second connection would be a second transaction, so a UDF called
        ///         inside one would answer from the wrong snapshot.
        ///     </para>
        ///     <para>
        ///         Dates are taken and returned as <c>string</c> rather than
        ///         <see cref="DateTime" />. SQLite has no date type: both this suite's stored
        ///         values and any parameter <c>Microsoft.Data.Sqlite</c> binds are TEXT in one
        ///         format, so comparing the text is what the store itself does, and the
        ///         alternative is to parse and reformat the same value twice for no gain.
        ///     </para>
        /// </remarks>
        private static void DefineFunctions(SqliteConnection connection)
        {
            connection.CreateFunction<int, long?>(
                "CustomerOrderCount",
                customerId => Scalar<long>(
                    connection,
                    @"SELECT count(""Id"") FROM ""Orders"" WHERE ""CustomerId"" = $customerId",
                    ("$customerId", customerId)));

            connection.CreateFunction<string?, long?>(
                "GetCustomerWithMostOrdersAfterDate",
                startDate => Scalar<long>(
                    connection,
                    @"SELECT ""CustomerId"" FROM ""Orders"" WHERE ""OrderDate"" > $startDate
                      GROUP BY ""CustomerId"" ORDER BY count(""Id"") DESC LIMIT 1",
                    ("$startDate", startDate)));

            // The period is ignored, as it is in EF's SqlServer fixture.
            connection.CreateFunction<int, string>(
                "GetReportingPeriodStartDate",
                _ => "1998-01-01 00:00:00",
                isDeterministic: true);

            connection.CreateFunction<int, long>(
                "IsTopCustomer",
                customerId => customerId == 1 ? 1 : 0,
                isDeterministic: true);

            // `IsDate` and `len` are mapped `IsBuiltIn()`, so the server emits them unqualified
            // and SQLite — which has neither — resolves them to these.
            connection.CreateFunction<string?, long>(
                "IsDate",
                value => DateTime.TryParse(value, CultureInfo.InvariantCulture, out _) ? 1 : 0,
                isDeterministic: true);

            connection.CreateFunction<string?, long?>(
                "len",
                value => value?.Length,
                isDeterministic: true);

            // Variadic, because the second argument is an `int` for `StarValue` and a `string`
            // for `DollarValue` and the mapped overloads differ only in that.
            connection.CreateFunction<string?>(
                "StarValue",
                arguments => Prefixed('*', arguments),
                isDeterministic: true);

            connection.CreateFunction<string?>(
                "DollarValue",
                arguments => Prefixed('$', arguments),
                isDeterministic: true);

            // AN INSTANCE FUNCTION, AND IT ONLY NEEDED DEFINING ONCE THE CALL STARTED ARRIVING.
            // `UDFSqlContext.StringLength` is mapped with `HasDbFunction` on a NON-static method,
            // so its receiver is the context. Until R151 such a call never reached a server: the
            // receiver held the live client context, which no wire carries, so the client ran the
            // method instead -- and its body throws, which is how EF proves it was translated
            // rather than run. The call arrives now, so the store has to have the function.
            //
            // It is declared as returning `string` in EF's base and the tests compare two results
            // to each other, so the value only has to be consistent. `null` in, `null` out, which
            // is what `PropagatesNullability` on the mapping says.
            connection.CreateFunction<string?, string?>(
                "StringLength",
                value => value?.Length.ToString(CultureInfo.InvariantCulture),
                isDeterministic: true);
        }

        /// <summary>
        ///     <c>replicate(@marker, @count) + @value</c>, which is how EF's SqlServer fixture
        ///     defines both <c>StarValue</c> and <c>DollarValue</c>.
        /// </summary>
        private static string? Prefixed(char marker, object?[] arguments)
            => arguments[0] is long count
                ? new string(marker, (int)count) + Convert.ToString(arguments[1], CultureInfo.InvariantCulture)
                : null;

        /// <summary>
        ///     The first column of the first row, or <see langword="null" /> when there is none.
        /// </summary>
        private static T? Scalar<T>(
            SqliteConnection connection,
            string sql,
            params (string Name, object? Value)[] parameters)
            where T : struct
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            foreach ((string name, object? value) in parameters)
            {
                command.Parameters.AddWithValue(name, value ?? (object)DBNull.Value);
            }

            object? result = command.ExecuteScalar();

            return result is null or DBNull
                ? null
                : (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        }
    }
}
