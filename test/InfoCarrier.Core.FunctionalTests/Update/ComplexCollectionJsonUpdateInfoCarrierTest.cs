// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;

namespace InfoCarrier.Core.FunctionalTests.Update;

/// <summary>
///     <c>ComplexCollectionJsonUpdateTestBase</c> on ADR-009 Tier B — the first spec coverage in
///     this suite of <em>writing</em> a collection mapped to JSON.
/// </summary>
/// <remarks>
///     <para>
///         C80 gave the client model the same key a JSON-mapped collection has on the store, which
///         made 36 `JsonQuery` tests right — but `JsonQueryTestBase` contains no `SaveChanges` at
///         all, so nothing said whether such a collection survives being written. This base does:
///         add, remove, reorder and replace an element, and edit a primitive collection inside one.
///     </para>
///     <para>
///         <b>Not <c>JsonUpdateTestBase</c>, and the reason is worth keeping.</b> That base is the
///         larger one (136 tests) and covers owned JSON collections directly, but its
///         <c>UseTransaction</c> is <c>public void</c> rather than <c>virtual</c>, and it calls the
///         relational <c>facade.UseTransaction(transaction.GetDbTransaction())</c>. A derived class
///         cannot replace it, and this provider's client is never a relational context, so all
///         **142** of its tests fail with the same sentence — *"Relational-specific methods can
///         only be used when the context is using a relational database provider"* — before
///         reaching anything about writing JSON. Measured, then reverted (C81). Here
///         <c>UseTransaction</c> <em>is</em> virtual, so the same corpus shape is reachable.
///     </para>
/// </remarks>
public class ComplexCollectionJsonUpdateInfoCarrierTest(
    ComplexCollectionJsonUpdateInfoCarrierTest.InfoCarrierFixture fixture)
    : ComplexCollectionJsonUpdateTestBase<ComplexCollectionJsonUpdateInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The base enlists a second context in the first's transaction, which is what a write
    ///     test needs; the relational route reaches for a <c>DbTransaction</c> the client does not
    ///     have. Same override, same reason, as <c>OptimisticConcurrencyInfoCarrierTest</c>'s.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <inheritdoc />
    /// <remarks>Nothing here asserts SQL, so there is no log to clear.</remarks>
    protected override void ClearLog()
    {
    }

    public class InfoCarrierFixture : ComplexCollectionJsonUpdateFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ComplexCollectionJsonUpdateInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
