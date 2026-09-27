// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>Ef6GroupByTestBase</c> on ADR-009 <b>Tier B</b> — EF6's own GroupBy corpus, ported by EF Core.
/// </summary>
/// <remarks>
///     <para>
///         `GroupBy` shapes are already named in the query residual as one of its three causes, and
///         the Northwind GroupBy base is the only thing measuring them. This is a second, independent
///         corpus over a different model, so it says whether that residual is Northwind-specific.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Base and fixture follow
///         EF's own SQLite class.
///     </para>
/// </remarks>
public class Ef6GroupByInfoCarrierTest(Ef6GroupByInfoCarrierTest.InfoCarrierFixture fixture)
    : Ef6GroupByTestBase<Ef6GroupByInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : Ef6GroupByFixtureBase, ITestSqlLoggerFactory
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;
    }
}
