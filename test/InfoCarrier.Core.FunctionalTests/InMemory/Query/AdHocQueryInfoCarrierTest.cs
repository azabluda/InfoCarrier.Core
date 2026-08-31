// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>AdHocMiscellaneousQueryTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     <para>
///         The <c>AdHoc*</c> bases are EF's regression corpus: each test is a model built for one
///         reported bug. They build that model <em>per test</em> rather than sharing a fixture,
///         which is why they need <see cref="NonSharedModelInfoCarrierHarness" /> (A49) and why
///         none of them was adoptable before it. The two overrides below are the whole of the
///         wiring, and are the same in every class of this kind.
///     </para>
///     <para>
///         This one stays on Tier A because its relational base is blocked: R47 read
///         <c>AdHocMiscellaneousQueryRelationalTestBase</c> and found it declares
///         <c>protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(…)</c>,
///         which EF's SQLite class implements on the <em>client's</em> options builder.
///     </para>
/// </remarks>
public class AdHocMiscellaneousQueryInfoCarrierTest(NonSharedFixture fixture)
    : AdHocMiscellaneousQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     The four <c>Task.CompletedTask</c> overrides are EF's own
    ///     <c>AdHocMiscellaneousQueryInMemoryTest</c>: they assert relational command caching and
    ///     query-cache behaviour that no non-relational provider has.
    /// </remarks>
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

    /// <inheritdoc />
    public override Task Explicitly_compiled_query_does_not_add_cache_entry()
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Inlined_dbcontext_is_not_leaking()
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Relational_command_cache_creates_new_entry_when_parameter_nullability_changes()
        => Task.CompletedTask;

    /// <inheritdoc />
    public override Task Variable_from_closure_is_parametrized()
        => Task.CompletedTask;
}

/// <inheritdoc cref="AdHocMiscellaneousQueryInfoCarrierTest" />
public class AdHocNavigationsQueryInfoCarrierTest(NonSharedFixture fixture)
    : AdHocNavigationsQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

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
