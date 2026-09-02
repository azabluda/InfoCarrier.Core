// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.NullSemanticsModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NullSemanticsQueryTestBase</c> on ADR-009 <b>Tier B</b> (#56, #60).
/// </summary>
/// <remarks>
///     <para>
///         The largest base left unadopted, and the one R41 said was worth the owner's attention:
///         about 168 test methods against a handful of permanent reds. R41 wrote a class, failed
///         to compile on the abstract member, and deleted it; R56 measures instead.
///     </para>
///     <para>
///         <b>The abstract member, and where the flag goes.</b> The base declares
///         <c>protected abstract NullSemanticsContext CreateContext(bool useRelationalNulls = false)</c>.
///         EF's SQLite class implements it as
///         <c>new SqliteDbContextOptionsBuilder(options).UseRelationalNulls()</c> — a relational
///         option on the <em>client's</em> builder, which <c>UseInfoCarrier</c> has none of (#60).
///         R56 accepted the flag and dropped it, and every test that passed <c>true</c> got C#
///         null semantics where it asked for the store's.
///     </para>
///     <para>
///         <b>R82 sends it to the server instead, which is where it belongs.</b>
///         <c>UseRelationalNulls</c> is ambient provider configuration: it decides the SQL, and the
///         SQL is the server's. The client cannot express it and should not try. What was missing
///         was not a client option but a way for one <em>request</em> to configure the server, and
///         the server's options were built once per store.
///         <see cref="SharedTestStoreProperties.ServerOptionsLifetime" /> is the seam — this
///         fixture is the only one that asks for transient server options — and
///         <see cref="RelationalNulls" /> is the ambient flag the request carries.
///     </para>
///     <para>
///         <b>This is a harness fix, not a product change.</b> A real application configures its
///         own server, so it already has this; the suite did not, because one server served every
///         test in the class. What the tests prove either way is the owner's stated rule: the
///         server's null semantics decide the caller's results.
///     </para>
/// </remarks>
public class NullSemanticsQueryInfoCarrierTest(NullSemanticsQueryInfoCarrierTest.InfoCarrierFixture fixture)
    : NullSemanticsQueryTestBase<NullSemanticsQueryInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     Whether the request in flight wants the store's null semantics.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>AsyncLocal</c> because one store serves every test in this class, and the tests
    ///         run concurrently. It is written by <see cref="CreateContext" /> — a synchronous
    ///         method, so the write is visible to the test that called it — and read by the
    ///         fixture's <c>onAddOptions</c> when the server builds a context for that test's
    ///         requests.
    ///     </para>
    ///     <para>
    ///         Not on the client context, and not carried by
    ///         <c>SharedTestStoreProperties.CopyDbContextParameters</c>, which is the other
    ///         per-request seam. That one runs after the server context exists, and an option
    ///         extension has to be in place before it is built.
    ///     </para>
    /// </remarks>
    internal static readonly AsyncLocal<bool> RelationalNulls = new();

    /// <inheritdoc />
    /// <remarks>
    ///     <c>useRelationalNulls</c> is recorded for the server rather than applied here: see the
    ///     class remarks. Everything else is EF's own <c>NullSemanticsQuerySqliteTest</c> body,
    ///     including the <c>NoTracking</c> that the base's assertions assume.
    /// </remarks>
    protected override NullSemanticsContext CreateContext(bool useRelationalNulls = false)
    {
        RelationalNulls.Value = useRelationalNulls;

        var context = new NullSemanticsContext(new DbContextOptionsBuilder(Fixture.CreateOptions()).Options);

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        return context;
    }

    public class InfoCarrierFixture : NullSemanticsQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "NullSemanticsQueryInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: ApplyRelationalNulls,
                configureConventions: ConfigureConventions,
                serverOptionsLifetime: ServiceLifetime.Transient,
                relationalClientStore: true,
                arbitrarySqlExecution: true);

        // The server half of the flag. Transient options mean this runs once per server context,
        // so it sees the value the test in flight set.
        private static DbContextOptionsBuilder ApplyRelationalNulls(DbContextOptionsBuilder builder)
        {
            if (RelationalNulls.Value)
            {
                new SqliteDbContextOptionsBuilder(builder).UseRelationalNulls();
            }

            return builder;
        }
    }
}
