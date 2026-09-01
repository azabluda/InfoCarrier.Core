// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
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
///         <b>106 tests: 30 pass, 75 red, 1 skipped by EF itself. Every red is one mechanism —
///         this provider does not support <c>HasDbFunction</c> — and NOT ONE is a wrong answer.</b>
///         The provider refuses the call at the client boundary or the funcletizer tries to
///         evaluate it locally; either way the caller gets an exception rather than a plausible
///         result. That is <b>#60's third shape</b> (R55, R56: <c>RelationalDbFunctions</c> refused
///         before the wire), and it is the safe failure mode.
///     </para>
///     <list type="table">
///         <item><description>36 — the client refuses the mapped call before the wire.</description></item>
///         <item><description>22 — the funcletizer tries to <em>evaluate</em> the UDF locally.</description></item>
///         <item><description>7 — a different exception or message than the base asserts.</description></item>
///         <item><description>4 — <c>NotImplementedException</c>: EF's UDF stub bodies, reached because the call was evaluated client-side. The same mechanism, seen from inside.</description></item>
///         <item><description>3 — EF's own translator marker exception, likewise client-side.</description></item>
///         <item><description>2 — <b>the store</b>: SQLite has no such user-defined function.</description></item>
///         <item><description>1 — the client-side part of the query.</description></item>
///     </list>
///     <para>
///         <b>Only 2 of 75 are SQLite's.</b> R71 recorded that the reds were "SQLite's missing
///         functions" and that was wrong; R73 corrected it by reading the reasons instead of the
///         base's name. EF ships no SQLite and no InMemory class for this base, which is
///         <c>CLAUDE.md</c>'s stated bar for leaving a base unadopted — but the bar's <em>reason</em>
///         is that such a base reports on the backing store rather than on the provider, and here
///         73 of 75 report on this provider. Adopted on the owner's decision for that reason.
///     </para>
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
///         <b>No functions are created here, unlike EF's SqlServer fixture.</b> SQLite has no
///         <c>CREATE FUNCTION</c>; <c>Microsoft.Data.Sqlite</c> can register one per
///         <em>connection</em> through <c>SqliteConnection.CreateFunction</c>, which is not a
///         schema object and would need a connection interceptor on the server. It would buy the
///         two store-side reds and none of the other 73, so it is priced and not taken.
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
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     <c>UdfFixtureBase.SeedAsync</c> only <em>stages</em> its entities — its last four
        ///     statements are <c>AddRange</c> calls and it never saves. EF's SqlServer fixture
        ///     finishes the job with its <c>create function</c> statements and a
        ///     <c>SaveChanges</c>; this one has no functions to create, so the save is the whole
        ///     of it. Without this the store is empty and all 106 tests read zero rows.
        /// </remarks>
        protected override async Task SeedAsync(DbContext context)
        {
            await base.SeedAsync(context);

            await context.SaveChangesAsync();
        }
    }
}
