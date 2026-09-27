// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>IncludeOneToOneTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         `Include` over a one-to-one, where the dependent shares its principal's key. That is the
///         shape L27's from-query tracking changed most — a reference navigation is fixed up from the
///         principal side and from the dependent side at once — and nothing adopted covers it
///         directly.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Base and fixture follow
///         EF's own SQLite class.
///     </para>
/// </remarks>
public class IncludeOneToOneInfoCarrierTest(IncludeOneToOneInfoCarrierTest.InfoCarrierFixture fixture)
    : IncludeOneToOneTestBase<IncludeOneToOneInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : OneToOneQueryFixtureBase, ITestSqlLoggerFactory
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
