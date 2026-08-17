// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestModels;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>MonsterFixupTestBase</c> on ADR-009 Tier A, in its snapshot-change-tracking form.
/// </summary>
/// <remarks>
///     The "monster" model is EF's largest: every relationship kind at once, seeded three
///     different ways — by foreign key, by navigation property, and by both — and then verified
///     from every end. That is precisely what this provider's client-side fixup has to get right,
///     because the graph is reassembled from the wire rather than by EF's shaper, and a
///     relationship that fixes up in one direction but not the other passes a smaller test.
///     <para>
///         The three <c>ValueGeneratedOnAdd</c> calls are EF's own
///         <c>MonsterFixupSnapshotInMemoryTest</c>'s and describe the backing store: InMemory
///         generates those keys at <c>Add</c> time (S3c-8), so the model has to say so.
///     </para>
/// </remarks>
public class MonsterFixupInfoCarrierTest(MonsterFixupInfoCarrierTest.MonsterFixupInfoCarrierFixture fixture)
    : MonsterFixupTestBase<MonsterFixupInfoCarrierTest.MonsterFixupInfoCarrierFixture>(fixture)
{
    public class MonsterFixupInfoCarrierFixture : MonsterFixupSnapshotFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        /// <remarks>
        ///     The context type is named rather than read off a <c>SharedStoreFixtureBase</c>:
        ///     <c>MonsterFixupFixtureBase</c> derives from <c>ServiceProviderFixtureBase</c>, which
        ///     has no <c>ContextType</c>, and states its context through <c>CreateContext</c>
        ///     instead. The server needs the same type the client builds.
        /// </remarks>
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                typeof(SnapshotMonsterContext),
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        protected override void OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(
            ModelBuilder builder)
        {
            base.OnModelCreating<TMessage, TProduct, TProductPhoto, TProductReview, TComputerDetail, TDimensions>(builder);

            builder.Entity<TMessage>().Property(e => e.MessageId).ValueGeneratedOnAdd();
            builder.Entity<TProductPhoto>().Property(e => e.PhotoId).ValueGeneratedOnAdd();
            builder.Entity<TProductReview>().Property(e => e.ReviewId).ValueGeneratedOnAdd();
        }
    }
}
