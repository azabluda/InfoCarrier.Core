// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>ManyToManyFieldsLoadTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     The same skip-navigation loading as `ManyToManyLoad`, over a field-only model — the
///     intersection of the two things this batch is aimed at.
/// </remarks>
public class ManyToManyFieldsLoadInfoCarrierTest(ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyFieldsLoadTestBase<ManyToManyFieldsLoadInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : ManyToManyFieldsLoadFixtureBase
    {
        // NO `TestSqlLoggerFactory`, and losing it is what the project split bought. It lives in
        // `EFCore.Relational.Specification.Tests`, which Tier A does not reference. It was here for
        // `RelationalComplianceTestBase`'s second assertion (R54), and Tier A is now checked by the
        // plain `ComplianceTestBase`, which does not ask. What it returned was the CLIENT's log
        // anyway, and this client has no database and emits no SQL.

        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ManyToManyFieldsLoadInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
