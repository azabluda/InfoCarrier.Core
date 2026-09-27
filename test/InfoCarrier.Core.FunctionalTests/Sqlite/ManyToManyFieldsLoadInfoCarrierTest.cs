// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ManyToManyFieldsLoadTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         The same skip-navigation loading as `ManyToManyLoad`, over a field-only model — the
///         intersection of the two things this batch is aimed at.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class ManyToManyFieldsLoadInfoCarrierTest(ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyFieldsLoadTestBase<ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyFieldsLoadFixtureBase, ITestSqlLoggerFactory
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ManyToManyFieldsLoadInfoCarrierTest";

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
