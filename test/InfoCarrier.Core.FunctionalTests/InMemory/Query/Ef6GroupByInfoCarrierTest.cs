// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>Ef6GroupByTestBase</c> on Tier A — EF6's own GroupBy corpus, ported by EF Core.
/// </summary>
/// <remarks>
///     `GroupBy` shapes are already named in the query residual as one of its three causes, and
///     the Northwind GroupBy base is the only thing measuring them. This is a second, independent
///     corpus over a different model, so it says whether that residual is Northwind-specific.
/// </remarks>
public class Ef6GroupByInfoCarrierTest(Ef6GroupByInfoCarrierTest.InfoCarrierFixture fixture)
    : Ef6GroupByTestBase<Ef6GroupByInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : Ef6GroupByFixtureBase, ITestSqlLoggerFactory
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

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
