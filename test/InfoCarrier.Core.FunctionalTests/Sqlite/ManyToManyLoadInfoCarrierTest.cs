// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ManyToManyLoadTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         Explicit and lazy loading of skip navigations. `ManyToManyTracking` is green, but it
///         saves and re-reads; this base loads, which is the half L7–L11 and L20–L21 rebuilt.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class ManyToManyLoadInfoCarrierTest(ManyToManyLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyLoadTestBase<ManyToManyLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyLoadFixtureBase, ITestSqlLoggerFactory
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ManyToManyLoadInfoCarrierTest";

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
