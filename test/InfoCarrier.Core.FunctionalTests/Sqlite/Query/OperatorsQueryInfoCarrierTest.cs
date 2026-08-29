// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     The operator matrix, on ADR-009 <b>Tier B</b> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>Generated combinations rather than hand-written queries.</b> These bases build a
///         large cross-product of operators over generated models — bitwise, comparison, logical,
///         string — and compare the result against LINQ to Objects. That makes them a wide net for
///         a wire that has to carry an operator tree faithfully, and they are cheap to adopt
///         because the model comes with them.
///     </para>
///     <para>
///         <b>Tier B</b> by ADR-009's rule: EF ships <c>OperatorsQuerySqliteTest</c> and no
///         InMemory counterpart. The base rather than EF's SQLite subclass, so no <c>AssertSql</c>
///         and no dialect comes with it.
///     </para>
/// </remarks>
public class OperatorsQueryInfoCarrierTest(NonSharedFixture fixture) : OperatorsQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}

/// <inheritdoc cref="OperatorsQueryInfoCarrierTest" />
public class OperatorsProceduralQueryInfoCarrierTest(NonSharedFixture fixture)
    : OperatorsProceduralQueryTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
