// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestModels.UpdatesModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.Update;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Update;

/// <summary>
///     <c>UpdatesRelationalTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own
///     <c>UpdatesSqliteTest</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>GraphUpdatesTestBase</c> covers reparenting a graph; this covers the plainer half —
///         inserting, updating and deleting single rows, concurrency tokens, and the messages EF
///         raises when a save finds the store changed underneath it.
///     </para>
///     <para>
///         <b>R42 moved this from Tier A to Tier B, and the move is what makes the messages
///         real.</b> On Tier A the concurrency messages were <c>InMemoryStrings</c>' and the class
///         had to say which of the sensitive/insensitive pair its backend composed. The relational
///         base states them itself, so both overrides are deleted; so is the reseed override, whose
///         remark said the InMemory store <i>"has no transaction to roll back"</i>, and the EF
///         issue #29875 override that was InMemory's alone.
///     </para>
/// </remarks>
public class UpdatesInfoCarrierTest(UpdatesInfoCarrierTest.UpdatesInfoCarrierFixture fixture)
    : UpdatesRelationalTestBase<UpdatesInfoCarrierTest.UpdatesInfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Required by CLAUDE.md's D6 rule and by the base itself, which declares
    ///     <c>UseTransaction(facade, t) => facade.UseTransaction(t.GetDbTransaction())</c> —
    ///     unreachable on a client with no database (ADR-013).
    /// </remarks>
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    /// <summary>
    ///     <c>UpdatesSqliteTest</c>'s: store-generated GUIDs are not supported on SQLite.
    /// </summary>
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Update/UpdatesSqliteTest.cs", 13, 15,
        Justification = "Store-generated guids are not supported",
        Skip = true)]
    public override Task Save_with_shared_foreign_key()
        => Task.CompletedTask;

    /// <summary>
    ///     The base declares this abstract, so every provider states it; the body is
    ///     <c>UpdatesSqliteTest</c>'s.
    /// </summary>
    /// <remarks>
    ///     <b>It asserts relational names on <c>context.Model</c>, and that model is the
    ///     client's.</b> Until 2026-09-15 this remark said the client does not build that model
    ///     relationally (M9), and the body asserted the table name alone, where EF's also asserts
    ///     the key, foreign-key and index names. R135 made every client relational, and EF's whole
    ///     body passes here, so it is EF's body now. An abstract test has no expectation to change,
    ///     so it carries no override reason.
    /// </remarks>
    public override void Identifiers_are_generated_correctly()
    {
        using UpdatesContext context = CreateContext();
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entityType = context.Model.FindEntityType(
            typeof(
                LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly))!;

        Assert.Equal(
            "LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly",
            entityType.GetTableName());
        Assert.Equal(
            "PK_LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly",
            entityType.GetKeys().Single().GetName());
        Assert.Equal(
            "FK_LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly_Profile_ProfileId_ProfileId1_ProfileId3_ProfileId4_ProfileId5_ProfileId6_ProfileId7_ProfileId8_ProfileId9_ProfileId10_ProfileId11_ProfileId12_ProfileId13_ProfileId14",
            entityType.GetForeignKeys().Single().GetConstraintName());
        Assert.Equal(
            "IX_LoginEntityTypeWithAnExtremelyLongAndOverlyConvolutedNameThatIsUsedToVerifyThatTheStoreIdentifierGenerationLengthLimitIsWorkingCorrectly_ProfileId_ProfileId1_ProfileId3_ProfileId4_ProfileId5_ProfileId6_ProfileId7_ProfileId8_ProfileId9_ProfileId10_ProfileId11_ProfileId12_ProfileId13_ProfileId14_ExtraProperty",
            entityType.GetIndexes().Single().GetDatabaseName());
    }

    /// <summary>
    ///     The updates fixture, wired to a SQLite backend behind the wire.
    /// </summary>
    public class UpdatesInfoCarrierFixture : UpdatesRelationalFixture
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override string StoreName
            => "UpdatesInfoCarrierTest";

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
