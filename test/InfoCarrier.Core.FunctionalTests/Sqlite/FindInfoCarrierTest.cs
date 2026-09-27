// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>FindTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own <c>FindSqliteTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>Find</c> is worth its own coverage here because it is the one read that may not reach
///         the server at all: a tracked entity is answered from the client's change tracker, and only
///         a miss becomes a query. The three nested classes are EF's, one per way of reaching
///         <c>Find</c>.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement.
///     </para>
/// </remarks>
public abstract class FindInfoCarrierTest(FindInfoCarrierTest.InfoCarrierFixture fixture)
    : FindTestBase<FindInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class FindInfoCarrierTestSet(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaSetFinder();
    }

    public class FindInfoCarrierTestContext(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaContextFinder();
    }

    public class FindInfoCarrierTestNonGeneric(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaNonGenericContextFinder();
    }

    public class InfoCarrierFixture : FindFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "FindInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     EF's own <c>FindSqliteFixture</c> configuration, word for word: the default composite
        ///     key of an owned collection does not work on SQLite (EF's issue #26708), so each gets a
        ///     table and a key of its own. Both halves build the model from this, as always.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<IntKey>(b =>
            {
                b.OwnsOne(
                    e => e.OwnedReference, b =>
                    {
                        b.OwnsOne(e => e.NestedOwned);
                        b.OwnsMany(e => e.NestedOwnedCollection).ToTable("NestedOwnedCollection").HasKey(e => e.Prop);
                    });

                b.OwnsMany(
                    e => e.OwnedCollection, b =>
                    {
                        b.ToTable("OwnedCollection").HasKey(e => e.Prop);
                        b.OwnsOne(e => e.NestedOwned);
                        b.OwnsMany(e => e.NestedOwnedCollection).ToTable("OwnedNestedOwnedCollection").HasKey(e => e.Prop);
                    });
            });
        }
    }
}
