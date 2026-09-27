// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>WithConstructorsTestBase</c> on ADR-009 <b>Tier B</b>: entities with no parameterless
///     constructor, bound by parameter name to properties, foreign keys and injected services.
/// </summary>
/// <remarks>
///     <para>
///         This is the base that aims at what L1 discovered indirectly. Building entities with
///         <c>Activator.CreateInstance</c> skips EF's materializer, so constructor binding and
///         service-property injection never run — which is why no entity had a working
///         <c>ILazyLoader</c> until the client started going through
///         <c>GetOrCreateMaterializer</c>. A constructor-bound model exercises that directly.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. The fixture is EF's own
///         <c>WithConstructorsSqliteTest</c>'s: the keyless <c>BlogQuery</c> reads its rows through
///         <c>ToSqlQuery</c>, and the update test runs in a transaction the server rolls back. On
///         Tier A a context of this class's own supplied the rows with <c>ToInMemoryQuery</c>, and
///         the update test reseeded the store through the backend.
///     </para>
/// </remarks>
public class WithConstructorsInfoCarrierTest(WithConstructorsInfoCarrierTest.InfoCarrierFixture fixture)
    : WithConstructorsTestBase<WithConstructorsInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's own SQLite class enlists the second context through the connection's transaction; a
    ///     client has no connection, so it enlists through the server's token.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseTestTransaction(transaction);

    public class InfoCarrierFixture : WithConstructorsFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "WithConstructorsInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<BlogQuery>().HasNoKey().ToSqlQuery("SELECT * FROM Blog");
        }
    }
}
