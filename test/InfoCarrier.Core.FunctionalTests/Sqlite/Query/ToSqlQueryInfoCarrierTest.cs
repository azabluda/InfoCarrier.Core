// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ToSqlQueryTestBase</c> on ADR-009 <b>Tier B</b> — an entity type mapped to a SQL query
///     rather than to a table, with a navigation off it.
/// </summary>
/// <remarks>
///     <para>
///         <b>Two tests, both green, and R62 priced this base as blocked twice over.</b> Its
///         verdict was that <c>builder.ToSqlQuery("SELECT * FROM PostStats")</c> is a relational
///         mapping on the <em>client's</em> model, which this provider does not build (M9), and
///         that the base declares a non-virtual
///         <c>UseTransaction(DatabaseFacade, IDbContextTransaction)</c> calling
///         <c>GetDbTransaction()</c> — ADR-013's blocking shape. <b>Neither fires.</b> R71
///         measured it by running the base instead of reading it.
///     </para>
///     <para>
///         Why the first does not fire: <c>ToSqlQuery</c> is model <em>metadata</em>. The client
///         records it, the server builds the same model from the same <c>OnModelCreating</c>, and
///         it is the server that turns the mapping into SQL — so nothing relational has to exist
///         on the client for the query to be answered. Why the second does not: no test in this
///         base routes through <c>UseTransaction</c>, which is exactly the distinction ADR-013's
///         2026-08-30 amendment draws — a non-virtual <c>UseTransaction</c> blocks a base only
///         when <em>every</em> route runs through it.
///     </para>
///     <para>
///         EF's <c>ToSqlQuerySqliteTest</c> adds a <c>Check_all_tests_overridden</c> and an
///         <c>AssertSql</c> on the one test. <b>Neither is taken</b>: both pin generated SQL, which
///         is the backend's business and not observable on a client that emits none (R54).
///     </para>
/// </remarks>
public class ToSqlQueryInfoCarrierTest(NonSharedFixture fixture) : ToSqlQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
