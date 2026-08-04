// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.InMemory.Internal;
using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Update;

/// <summary>
///     <c>UpdatesTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     <para>
///         `GraphUpdatesTestBase` covers reparenting a graph; this covers the plainer half —
///         inserting, updating and deleting single rows, concurrency tokens, and the messages EF
///         raises when a save finds the store changed underneath it. Every one of those messages
///         comes from the *backing store* here, so they are InMemory's, exactly as EF's own
///         `UpdatesInMemoryTestBase` asserts them.
///     </para>
///     <para>
///         Both overrides below are EF's own, from `UpdatesInMemoryTestBase` and
///         `UpdatesInMemoryWithoutSensitiveDataLoggingTest`: the reseed after each transactional
///         test, because the InMemory store has no transaction to roll back, and issue #29875.
///     </para>
/// </remarks>
public class UpdatesInfoCarrierTest(UpdatesInfoCarrierTest.UpdatesInfoCarrierFixture fixture)
    : UpdatesTestBase<UpdatesInfoCarrierTest.UpdatesInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    protected override string UpdateConcurrencyMessage
        => InMemoryStrings.UpdateConcurrencyException;

    /// <inheritdoc />
    protected override string UpdateConcurrencyTokenMessage
        => InMemoryStrings.UpdateConcurrencyTokenException("Product", "{'Price'}");

    /// <inheritdoc />
    /// <remarks>EF issue #29875, on its own InMemory suite too.</remarks>
    public override Task Can_change_type_of_pk_to_pk_dependent_by_replacing_with_new_dependent(bool async)
        => Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => base.Can_change_type_of_pk_to_pk_dependent_by_replacing_with_new_dependent(async));

    /// <inheritdoc />
    /// <remarks>
    ///     The InMemory store has no transaction, so a test that expects its changes rolled back
    ///     needs the data put back by hand. EF's `UpdatesInMemoryTestBase` does exactly this.
    /// </remarks>
    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<UpdatesContext, Task> testOperation,
        Func<UpdatesContext, Task>? nestedTestOperation1 = null,
        Func<UpdatesContext, Task>? nestedTestOperation2 = null)
    {
        try
        {
            await base.ExecuteWithStrategyInTransactionAsync(testOperation, nestedTestOperation1, nestedTestOperation2);
        }
        finally
        {
            await Fixture.ReseedAsync();
        }
    }

    public class UpdatesInfoCarrierFixture : UpdatesFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "UpdatesInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     `TransactionIgnoredWarning` is logged rather than thrown because the backing store is
        ///     InMemory, and the insensitive message above is what the base asserts.
        /// </remarks>
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(w => w.Log(InMemoryEventId.TransactionIgnoredWarning))
                .EnableSensitiveDataLogging(false);
    }
}
