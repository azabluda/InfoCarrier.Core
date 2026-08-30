// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>DataAnnotationRelationalTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         Mostly a model-building suite — which annotation produces which metadata — and
///         therefore a direct check that the client model this provider builds is EF's, since the
///         client is where conventions actually run.
///     </para>
///     <para>
///         <b>Moved from Tier A under the Group C policy.</b> The move deletes six overrides the
///         Tier A class carried: each replaced a round trip with a metadata assertion because
///         InMemory enforces no store constraint, and a real database enforces all six. The
///         relational base adds two tests of its own and a fixture that maps <c>Animal</c>,
///         <c>Pet</c>, <c>Cat</c> and <c>Dog</c> as TPT.
///     </para>
/// </remarks>
public class DataAnnotationInfoCarrierTest(DataAnnotationInfoCarrierTest.DataAnnotationInfoCarrierFixture fixture)
    : DataAnnotationRelationalTestBase<DataAnnotationInfoCarrierTest.DataAnnotationInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Written in the same commit as the store switch, per CLAUDE.md. The base routes five
    ///     tests through <c>ExecuteWithStrategyInTransactionAsync</c> — four in the core base and
    ///     one in the relational one — and that helper opens a single transaction every other
    ///     context must enlist in. On Tier A the transaction was ignored and its absence showed
    ///     nowhere; on Tier B it is real, and an unenlisted context waits out SQLite's lock.
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <inheritdoc />
    protected override TestHelpers TestHelpers
        => InfoCarrierTestHelpers.Instance;

    /// <inheritdoc />
    /// <remarks>
    ///     Unlike InMemory's, which answers <see langword="false" />: this provider keeps EF's
    ///     `ForeignKeyIndexConvention`, so its model has the indexes (A47 measured what happens
    ///     when the flag says otherwise — 136 failures).
    /// </remarks>
    protected override bool HasForeignKeyIndexes
        => true;

    // The three below are EF's own `DataAnnotationSqliteTest` overrides, and the Tier A class
    // carried the same three with the same bodies for InMemory. The store changed; the reason did
    // not, so this is convergence with the reference provider rather than a workaround.

    /// <inheritdoc />
    /// <remarks>SQLite does not enforce a column length.</remarks>
    public override Task MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length()
    {
        using DbContext context = CreateContext();
        Assert.Equal(10, context.Model.FindEntityType(typeof(One))!.FindProperty("MaxLengthProperty")!.GetMaxLength());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>SQLite does not enforce a column length.</remarks>
    public override Task StringLengthAttribute_throws_while_inserting_value_longer_than_max_length()
    {
        using DbContext context = CreateContext();
        Assert.Equal(16, context.Model.FindEntityType(typeof(Two))!.FindProperty("Data")!.GetMaxLength());
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>SQLite has no <c>rowversion</c>. EF issue #2195, the same one this repo's
    ///     <c>OptimisticConcurrencyInfoCarrierTest</c> skips eleven tests for.</remarks>
    public override Task TimestampAttribute_throws_if_value_in_database_changed()
    {
        using DbContext context = CreateContext();
        Assert.True(context.Model.FindEntityType(typeof(Two))!.FindProperty("Timestamp")!.IsConcurrencyToken);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     The data-annotation fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class DataAnnotationInfoCarrierFixture : DataAnnotationRelationalFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "DataAnnotationInfoCarrierTest";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
