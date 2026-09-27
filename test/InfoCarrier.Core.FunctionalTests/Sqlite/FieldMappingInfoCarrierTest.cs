// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>FieldMappingTestBase</c> on ADR-009 <b>Tier B</b>: entities whose state lives in backing
///     fields rather than properties, including read-only collections and fields with no property
///     at all.
/// </summary>
/// <remarks>
///     <para>
///         Adopted because this provider reads and writes through backing fields in several places
///         it had to learn the hard way — L6 (a navigation is read through its field, never its
///         property, or reading it *is* a lazy load) and L18 (a shadow navigation has no member at
///         all). Those were found through other bases; this one aims at the behaviour directly.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. The update tests now run
///         in a transaction the server rolls back, as in EF's own <c>FieldMappingSqliteTest</c>;
///         on Tier A they reseeded the store through the backend instead.
///     </para>
/// </remarks>
public class FieldMappingInfoCarrierTest(FieldMappingInfoCarrierTest.InfoCarrierFixture fixture)
    : FieldMappingTestBase<FieldMappingInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     EF's own SQLite class enlists the second context through the connection's transaction; a
    ///     client has no connection, so it enlists through the server's token.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseTestTransaction(transaction);

    public class InfoCarrierFixture : FieldMappingFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "FieldMappingInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
