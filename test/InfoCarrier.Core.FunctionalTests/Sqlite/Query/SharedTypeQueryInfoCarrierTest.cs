// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>SharedTypeQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b> — a model whose entity
///     types are keyed by name rather than by CLR type, so several share one
///     <see cref="System.Collections.Generic.Dictionary{TKey,TValue}" />.
/// </summary>
/// <remarks>
///     <para>
///         <b>Moved from Tier A rather than added beside it (R83).</b>
///         <c>SharedTypeQueryRelationalTestBase</c> derives from <c>SharedTypeQueryTestBase</c>,
///         which this class used to take on InMemory. Adopting the relational base as a second
///         class would have run the shared base's tests on both tiers, and CLAUDE.md is explicit
///         that a base belongs to exactly one tier. Where a base could go either way, the tier that
///         <em>translates</em> is the one whose green means more, so the whole class moves.
///     </para>
///     <para>
///         Non-shared-model, so it goes through <see cref="NonSharedModelInfoCarrierHarness" />
///         (A49). EF's InMemory class adds a <c>ToInMemoryQuery</c> test of its own; that is the
///         store's, not the spec base's, and is not carried (A47). EF's own SQLite class overrides
///         nothing at all, so neither does this one.
///     </para>
/// </remarks>
public class SharedTypeQueryInfoCarrierTest(NonSharedFixture fixture)
    : SharedTypeQueryRelationalTestBase(fixture)
{
    // Both opt-ins, for the two reds this class carries. `SharedTypeQueryRelationalTestBase` casts
    // the store to `RelationalTestStore` in `Ad_hoc_query_for_shared_type_entity_type_works`, and
    // the query it then builds is a `FromSql` (#60).
    private readonly NonSharedModelInfoCarrierHarness _harness = new(
        InfoCarrierTestStoreFactory.Sqlite,
        relationalClientStore: true,
        arbitrarySqlExecution: true);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

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
