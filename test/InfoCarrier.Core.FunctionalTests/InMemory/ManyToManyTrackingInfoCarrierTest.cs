// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.ManyToManyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>ManyToManyTrackingTestBase</c> on ADR-009 Tier A, mirroring EF's own
///     <c>ManyToManyTrackingInMemoryTest</c>.
/// </summary>
/// <remarks>
///     The join entity of a many-to-many has foreign keys but no navigations to link through, so
///     a skip navigation's changes reach the wire as join rows whose keys are both placeholders.
///     S3c-9 built the placeholder machinery on exactly that case; this is the spec coverage for
///     it.
/// </remarks>
public class ManyToManyTrackingInfoCarrierTest(ManyToManyTrackingInfoCarrierTest.InfoCarrierFixture fixture)
    : ManyToManyTrackingTestBase<ManyToManyTrackingInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's InMemory base does the same, and for the same reason this repo already reseeds in
    ///     <c>GraphUpdatesInfoCarrierTest</c>: without a real transaction there is no rollback to
    ///     undo the test's mutations.
    /// </remarks>
    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<ManyToManyContext, Task> testOperation,
        Func<ManyToManyContext, Task> nestedTestOperation1 = null,
        Func<ManyToManyContext, Task> nestedTestOperation2 = null,
        Func<ManyToManyContext, Task> nestedTestOperation3 = null)
    {
        // `finally`, because a *failing* test dirties the store exactly as a passing one does.
        // Reseeding only on success let the first parameterization of a method leave its rows
        // behind for the second, which then failed on "an item with the same key has already
        // been added" — an error about the previous test, reported against this one.
        try
        {
            await base.ExecuteWithStrategyInTransactionAsync(
                testOperation, nestedTestOperation1, nestedTestOperation2, nestedTestOperation3);
        }
        finally
        {
            await Fixture.ReseedAsync();
        }
    }

    // The backend is the InMemory store, which has no database default values.
    protected override bool SupportsDatabaseDefaults
        => false;

    public class InfoCarrierFixture : ManyToManyTrackingFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "ManyToManyTrackingInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Log(InfoCarrierEventId.TransactionIgnoredWarning))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

        /// <summary>
        ///     Reseeds through the <em>backend</em> context rather than the client one.
        /// </summary>
        /// <remarks>
        ///     The base seeds through a client context, which would make every test's setup
        ///     depend on remoted SaveChanges — the thing under test. The initial seed already
        ///     runs server-side, so this keeps seeding to one mechanism.
        ///     <c>GraphUpdatesInfoCarrierTest</c> does the same.
        /// </remarks>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((ManyToManyContext)context);
        }
    }
}
