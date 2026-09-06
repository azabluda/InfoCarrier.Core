// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>NonSharedPrimitiveCollectionsQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     One model per element type — <c>int[]</c>, <c>List&lt;DateTime&gt;</c>, an enum array, a
///     nullable one — each queried through <c>Count</c>, <c>Contains</c> and an indexer, which is
///     the question of whether a collection-valued property survives the round trip at all.
///     <para>
///         <b>Tier B</b>, by the rule A79 established: EF ships
///         <c>NonSharedPrimitiveCollectionsQuerySqliteTest</c> and no InMemory counterpart, because
///         a primitive collection is a thing a store either maps or does not. The core base rather
///         than the relational one, which asserts SQL a client with no database does not have.
///     </para>
/// </remarks>
public class NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : NonSharedPrimitiveCollectionsQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     A no-op. EF's SQLite writes
    ///     <c>new SqliteDbContextOptionsBuilder(o).UseParameterizedCollectionMode(...)</c>, a
    ///     relational option on the <em>client's</em> builder that this provider does not have.
    ///     The six <c>*_with_default_mode_EF_MultipleParameters</c> tests that ask for a
    ///     non-default mode are red because of it, and they are #60's fourth shape rather than a
    ///     translation gap: the query is right and the knob to request it is missing.
    /// </remarks>
    protected override DbContextOptionsBuilder SetParameterizedCollectionMode(
        DbContextOptionsBuilder optionsBuilder,
        ParameterTranslationMode parameterizedCollectionMode)
        => optionsBuilder;

    /// <summary>
    ///     EF's own skip, adopted verbatim (C94). <c>NonSharedPrimitiveCollectionsQuerySqliteTest</c>
    ///     carries this attribute and this reason.
    /// </summary>
    /// <remarks>
    ///     <b>Issue #30730 is SQLite's</b> — EF's SQL Server suite skips it under a different
    ///     issue and for a different reason, and this class is Tier B, so adopting the attribute
    ///     here loses nothing when M7 brings a Tier C. C62 established the same conclusion by
    ///     reading the row the store actually holds; this records it the way EF does.
    /// </remarks>
    [ConditionalFact(Skip = "Issue #30730: TODO: SQLite is not matching elements here.")]
    public override Task Array_of_TimeOnly()
        => base.Array_of_TimeOnly();

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
