// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>FunkyDataQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         String data chosen to break naive predicate translation — embedded wildcards, escapes,
///         empty strings, nulls — pushed through <c>Contains</c>, <c>StartsWith</c> and
///         <c>EndsWith</c>. The question it asks is whether a predicate still means the same thing
///         after a round trip, which is squarely this provider's business.
///     </para>
///     <para>
///         <b>Tier B, and that is the point.</b> A70 adopted this on Tier A and reverted it: 34 of
///         38 died inside EF's own InMemory provider on
///         <c>String.EndsWith(null, StringComparison)</c>, because that provider client-evaluates
///         the operators unguarded. EF ships no InMemory counterpart for this base and does ship a
///         SQLite one — which is what Tier B exists for (ADR-009). The corpus is about translation,
///         so it belongs on the tier that translates.
///     </para>
/// </remarks>
public class FunkyDataQuerySqliteInfoCarrierTest(
    FunkyDataQuerySqliteInfoCarrierTest.FunkyDataQuerySqliteInfoCarrierFixture fixture)
    : FunkyDataQueryTestBase<FunkyDataQuerySqliteInfoCarrierTest.FunkyDataQuerySqliteInfoCarrierFixture>(fixture)
{
    public class FunkyDataQuerySqliteInfoCarrierFixture : FunkyDataQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "FunkyDataQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
