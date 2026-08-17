// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>NotificationEntitiesTestBase</c> on Tier A: a model using
///     <c>ChangeTrackingStrategy.ChangingAndChangedNotifications</c>, where EF never calls
///     <c>DetectChanges</c> and relies on the entities to report their own edits.
/// </summary>
/// <remarks>
///     Worth adopting because this provider's client tracker is populated by hand rather than by
///     EF's shaper, and a notification model is the one where nothing re-derives what was missed:
///     a navigation assigned without telling the tracker simply never becomes a change.
/// </remarks>
public class NotificationEntitiesInfoCarrierTest(NotificationEntitiesInfoCarrierTest.InfoCarrierFixture fixture)
    : NotificationEntitiesTestBase<NotificationEntitiesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : NotificationEntitiesFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "NotificationEntitiesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
