// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>LoadTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own <c>LoadSqliteTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         Explicit loading — <c>Entry(e).Collection(...).LoadAsync()</c> and friends — is a query
///         built from a <em>tracked</em> entity's key and fixed up into a graph the client already
///         holds. Nothing before this exercised that path.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class LoadInfoCarrierTest(LoadInfoCarrierTest.InfoCarrierFixture fixture)
    : LoadTestBase<LoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : LoadFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "LoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
