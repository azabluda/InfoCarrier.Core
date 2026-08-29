// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>StoreGeneratedFixupRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         Fixup between entities whose keys the store generates — the case where the client's key
///         is temporary and cannot travel (wire-protocol W2).
///     </para>
///     <para>
///         <b>Moved from Tier A, and this move deletes two workarounds rather than adding one.</b>
///         The Tier A class declared <c>EnforcesFKs => false</c>, because InMemory does not enforce
///         foreign keys, and overrode <c>ExecuteWithStrategyInTransactionAsync</c> to empty the
///         store by hand, because InMemory has no transaction to roll back. On SQLite both facts
///         reverse: foreign keys are enforced, so the constraint tests become real, and the
///         rollback undoes each test, so the manual clean is unnecessary.
///     </para>
///     <para>
///         <b>The <c>UseTransaction</c> override is what makes that rollback reach the inner
///         contexts, and the base calls the transaction helper 118 times.</b> It is
///         <c>protected virtual</c> with an empty body on the core base — checked, not assumed —
///         so an override reaches it. EF's own SQLite class supplies
///         <c>transaction.GetDbTransaction()</c>, which ADR-013 makes unreachable here, so this one
///         supplies <c>UseInfoCarrierTransaction</c> instead. Without it every inner context would
///         sit outside the transaction the outer one holds the write lock for.
///     </para>
/// </remarks>
public class StoreGeneratedFixupInfoCarrierTest(StoreGeneratedFixupInfoCarrierTest.InfoCarrierFixture fixture)
    : StoreGeneratedFixupRelationalTestBase<StoreGeneratedFixupInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>The backing store is SQLite, which enforces them. EF's own SQLite class agrees.</remarks>
    protected override bool EnforcesFKs
        => true;

    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <summary>
    ///     The store-generated-fixup fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class InfoCarrierFixture : StoreGeneratedFixupRelationalFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "StoreGeneratedFixupInfoCarrierTest";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
