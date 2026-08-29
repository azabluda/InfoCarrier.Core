// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>ComplexTypesTrackingRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The corpus for complex types on the wire. A complex property is not in
///         <c>IEntityType.GetProperties()</c>, so before this base was adopted nothing in the suite
///         could tell whether one travelled at all.
///     </para>
///     <para>
///         <b>Moved from Tier A, and the move deletes two InMemory accommodations.</b> The class it
///         replaces reseeded the store after every test, because InMemory has no transaction to
///         roll back, and logged <c>InMemoryEventId.TransactionIgnoredWarning</c> because
///         transactions were being ignored. On SQLite the transaction is real, so the rollback
///         cleans and the warning has nothing to say.
///     </para>
///     <para>
///         <b>The <c>UseTransaction</c> override is what makes that true.</b> The base calls the
///         transaction helper three times and declares <c>UseTransaction</c> <c>protected
///         virtual</c> — checked, not assumed. The relational base overrides it with
///         <c>GetDbTransaction()</c>, which ADR-013 makes unreachable on this client, so this class
///         overrides it again with <c>UseInfoCarrierTransaction</c>.
///     </para>
/// </remarks>
public class ComplexTypesTrackingInfoCarrierTest(
    ComplexTypesTrackingInfoCarrierTest.InfoCarrierFixture fixture,
    ITestOutputHelper testOutputHelper)
    : ComplexTypesTrackingRelationalTestBase<ComplexTypesTrackingInfoCarrierTest.InfoCarrierFixture>(
        fixture,
        testOutputHelper)
{
    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <summary>
    ///     The complex-types fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class InfoCarrierFixture : RelationalFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "ComplexTypesTrackingInfoCarrierTest";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     Carried over from the Tier A fixture. It is a choice this repository made about how
        ///     the model reads its values, not an accommodation for the InMemory store, so the
        ///     store change does not retire it.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            modelBuilder.UsePropertyAccessMode(PropertyAccessMode.PreferProperty);
            base.OnModelCreating(modelBuilder, context);
        }
    }
}
