// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels.ConferencePlanner;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>ConferencePlannerTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     The second application-shaped suite after <c>MusicStore</c>, and the more useful of the two
///     here: every test is a controller action — load, project into a DTO, mutate, save — run
///     against a context per operation. That is the combination a per-feature base never quite
///     reaches, and it is the shape a real caller of this provider writes.
/// </remarks>
public class ConferencePlannerInfoCarrierTest(ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture fixture)
    : ConferencePlannerTestBase<ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The base relies on a real transaction rolling each test back; Tier A's store has none,
    ///     so a test that renames or removes a session leaves it renamed and the next one fails
    ///     looking for it — `SessionsController_Get_with_ID` came up "Sequence contains no
    ///     elements", and `AttendeesController_AddSession` counted 20 where it wanted 21. Putting
    ///     the data back afterwards is what `GraphUpdatesInfoCarrierTest`,
    ///     `UpdatesInfoCarrierTest` and `ProxyGraphUpdatesInfoCarrierTest` all do for the same
    ///     reason.
    /// </remarks>
    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<ApplicationDbContext, Task> testOperation,
        Func<ApplicationDbContext, Task>? nestedTestOperation1 = null,
        Func<ApplicationDbContext, Task>? nestedTestOperation2 = null,
        Func<ApplicationDbContext, Task>? nestedTestOperation3 = null)
    {
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

    public class ConferencePlannerInfoCarrierFixture : ConferencePlannerFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ConferencePlannerInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));

        /// <summary>
        ///     Reseeds through the <em>backend</em> context (A74/A75: the client side of these
        ///     APIs is a no-op by construction).
        /// </summary>
        public override async Task ReseedAsync()
        {
            InfoCarrierBackendTestStore backend = ((InfoCarrierTestStore)TestStore).Backend;
            using DbContext context = backend.CreateDbContext();
            await backend.CleanAsync(context);
            await CleanAsync(context);
            await SeedAsync((ApplicationDbContext)context);
        }
    }
}
