// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>EntitySplittingTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         The write side of entity splitting. <c>EntitySplittingQueryInfoCarrierTest</c> covers
///         reading one entity back out of several tables; this covers saving one into them, which
///         nothing else in the suite does.
///     </para>
///     <para>
///         <b>Its second test reaches its operation only because it is written out, and the reason
///         is ADR-013.</b> <c>ExecuteDelete_throws_for_entity_splitting</c> calls
///         <c>TestHelpers.ExecuteWithStrategyInTransactionAsync</c> inline, passing the base's
///         <c>public void UseTransaction</c>, which calls <c>GetDbTransaction()</c>. There is no
///         virtual hook between the test and that member. Until 2026-09-15 it was left failing on
///         that call and never reached <c>ExecuteDelete</c> at all.
///     </para>
/// </remarks>
public class EntitySplittingInfoCarrierTest(NonSharedFixture fixture, ITestOutputHelper testOutputHelper)
    : EntitySplittingTestBase(fixture, testOutputHelper)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     The base's body, with this provider's transaction in place of the base's non-virtual
    ///     <c>UseTransaction</c>. The assertion is EF's own.
    /// </remarks>
    [InfoCarrierDesign(
        13,
        Justification = "The client has no DbTransaction, and the base's own UseTransaction helper is not virtual and asks "
            + "for one, so the test never reaches the operation it is named for.",
        Deviation = DeviationKind.QueryWrittenOut,
        DeviationNote = "The body is the base's, with InfoCarrier's transaction passed to ExecuteWithStrategyInTransactionAsync.")]
    public override async Task ExecuteDelete_throws_for_entity_splitting(bool async)
    {
        await InitializeAsync(OnModelCreating, sensitiveLogEnabled: true);

        await TestHelpers.ExecuteWithStrategyInTransactionAsync(
            CreateContext,
            (facade, transaction) => facade.UseTestTransaction(transaction),
            async context => Assert.Contains(
                CoreStrings.NonQueryTranslationFailedWithDetails(
                    "", RelationalStrings.ExecuteOperationOnEntitySplitting("ExecuteDelete", "MeterReading"))[21..],
                (await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                {
                    if (async)
                    {
                        await context.MeterReadings.ExecuteDeleteAsync();
                    }
                    else
                    {
                        context.MeterReadings.ExecuteDelete();
                    }
                })).Message));
    }

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
