// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>ManyToManyLoadTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     Explicit and lazy loading of skip navigations. `ManyToManyTracking` is green, but it
///     saves and re-reads; this base loads, which is the half L7–L11 and L20–L21 rebuilt.
/// </remarks>
public class ManyToManyLoadInfoCarrierTest(ManyToManyLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyLoadTestBase<ManyToManyLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyLoadFixtureBase, ITestSqlLoggerFactory
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

        protected override string StoreName
            => "ManyToManyLoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
