// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>NotificationEntitiesTestBase</c> on ADR-009 <b>Tier B</b>: a model using
///     <c>ChangeTrackingStrategy.ChangingAndChangedNotifications</c>, where EF never calls
///     <c>DetectChanges</c> and relies on the entities to report their own edits.
/// </summary>
/// <remarks>
///     <para>
///         Worth adopting because this provider's client tracker is populated by hand rather than by
///         EF's shaper, and a notification model is the one where nothing re-derives what was missed:
///         a navigation assigned without telling the tracker simply never becomes a change.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public class NotificationEntitiesInfoCarrierTest(NotificationEntitiesInfoCarrierTest.InfoCarrierFixture fixture)
    : NotificationEntitiesTestBase<NotificationEntitiesInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : NotificationEntitiesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "NotificationEntitiesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
