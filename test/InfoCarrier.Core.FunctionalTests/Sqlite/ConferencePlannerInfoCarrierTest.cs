// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ConferencePlannerTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         The second application-shaped suite after <c>MusicStore</c>, and the more useful of the
///         two: every test is a controller action — load, project into a DTO, mutate, save — run
///         against a context per operation. That is the combination a per-feature base never quite
///         reaches, and it is the shape a real caller of this provider writes.
///     </para>
///     <para>
///         <b>Tier B, corrected in A80.</b> A70 put it on Tier A; the audit found EF ships a SQLite
///         test for this base and <em>no</em> InMemory one, so Tier A was never the right home. The
///         evidence was already in hand and misread: A76 had to add a reseed-after-every-test
///         override because the base wraps each test in a transaction and Tier A has none. Here the
///         transaction is real, <see cref="UseTransaction" /> enlists the second context in it, and
///         the workaround is deleted rather than kept.
///     </para>
/// </remarks>
public class ConferencePlannerInfoCarrierTest(ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture fixture)
    : ConferencePlannerTestBase<ConferencePlannerInfoCarrierTest.ConferencePlannerInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     The base opens a transaction on one context and makes a second observe the same
    ///     uncommitted state. Without enlisting, the second runs on its own SQLite connection and
    ///     gets "database is locked" — the same hook, and the same reason,
    ///     <c>OptimisticConcurrencyInfoCarrierTest</c> needs it.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    public class ConferencePlannerInfoCarrierFixture : ConferencePlannerFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ConferencePlannerInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
