// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>TPTTableSplittingTestBase</c> on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Two entity types in one table, under a hierarchy mapped to several.</b> Table
///         splitting and TPT pull in opposite directions — one merges types into a store object,
///         the other spreads a hierarchy across them — and this base is where they meet. It is the
///         last of the TPT and TPC family, and it closes <c>TableSplittingTestBase</c> on the same
///         chain.
///     </para>
///     <para>
///         <b>The non-shared-model shape, not the shared-store one.</b> Every other inheritance
///         base adopted here takes a fixture that owns a store; this one builds a model per test
///         through <c>NonSharedModelTestBase</c>, so it uses
///         <c>NonSharedModelInfoCarrierHarness</c> as
///         <c>NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest</c> does.
///     </para>
/// </remarks>
public class TPTTableSplittingInfoCarrierTest(NonSharedFixture fixture, ITestOutputHelper testOutputHelper)
    : TPTTableSplittingTestBase(fixture, testOutputHelper)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own override, verbatim from <c>TPTTableSplittingSqliteTest</c> and for EF's stated
    ///     reason: the scenario is not valid for TPT. Adopted rather than invented.
    /// </remarks>
    public override Task Can_insert_dependent_with_just_one_parent()
        => Task.CompletedTask;

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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
