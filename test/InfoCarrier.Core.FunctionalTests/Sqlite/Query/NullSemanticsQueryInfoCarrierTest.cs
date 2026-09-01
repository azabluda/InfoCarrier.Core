// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.NullSemanticsModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

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
///         <b>The abstract member, and why the flag is ignored.</b> The base declares
///         <c>protected abstract NullSemanticsContext CreateContext(bool useRelationalNulls = false)</c>.
///         EF's SQLite class implements it as
///         <c>new SqliteDbContextOptionsBuilder(options).UseRelationalNulls()</c> — a relational
///         option on the <em>client's</em> builder, which <c>UseInfoCarrier</c> has none of (#60).
///         So the flag is accepted and dropped, and every test that passes <c>true</c> gets C#
///         null semantics where it asked for the store's. Those tests are the cost, and counting
///         them is the whole point of the step.
///     </para>
/// </remarks>
public class NullSemanticsQueryInfoCarrierTest(NullSemanticsQueryInfoCarrierTest.InfoCarrierFixture fixture)
    : NullSemanticsQueryTestBase<NullSemanticsQueryInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     <c>useRelationalNulls</c> is deliberately ignored: see the class remarks. Everything
    ///     else is EF's own <c>NullSemanticsQuerySqliteTest</c> body, including the
    ///     <c>NoTracking</c> that the base's assertions assume.
    /// </remarks>
    protected override NullSemanticsContext CreateContext(bool useRelationalNulls = false)
    {
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
                configureConventions: ConfigureConventions,
                relationalClientStore: true);
    }
}
