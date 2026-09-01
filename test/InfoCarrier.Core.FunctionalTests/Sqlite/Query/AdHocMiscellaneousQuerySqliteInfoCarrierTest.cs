// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>AdHocMiscellaneousQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         The <c>AdHoc*</c> bases are EF's regression corpus: each test is a model built for one
///         reported bug, built <em>per test</em> rather than shared, which is why they need
///         <see cref="NonSharedModelInfoCarrierHarness" /> (A49).
///     </para>
///     <para>
///         <b>A tier MOVE, and R62's blocker does not exist.</b> The class was on Tier A because
///         R51 read <c>protected abstract DbContextOptionsBuilder SetParameterizedCollectionMode(…)</c>
///         — which EF's SQLite implements on the <em>client's</em> options builder — and called
///         the base blocked. R71 measured it instead: a <b>no-op</b> implementation is enough, and
///         the base then yields 11 new green tests against 2 red. The member is only consulted by
///         tests that ask for a non-default mode, and this base has none.
///     </para>
///     <para>
///         <b>The four <c>Task.CompletedTask</c> overrides move across unchanged, and they are not
///         a tier artefact.</b> They are EF's own <c>AdHocMiscellaneousQueryInMemoryTest</c>'s, for
///         tests that assert the size of EF's <b>relational command cache</b> — and it is the
///         <em>client's</em> cache they read. This client is not a relational provider on either
///         tier, so the reason those overrides exist is untouched by the backing store; EF's SQLite
///         class does not carry them because a real relational client has that cache.
///         <b>Dropping them here would have turned four passing tests red and looked like a
///         regression of the move.</b>
///     </para>
///     <para>
///         <b>The two reds are R71's <c>FromSql</c> defect and nothing else.</b>
///         <c>Multiple_different_entity_type_from_different_namespaces</c> is a
///         <c>FromSqlRaw("SELECT cast(null as int) AS MyValue")</c>, and this provider discards the
///         query root rather than running or refusing it, so the exception the test exists to
///         provoke never arrives. Left red on purpose: it is the cheapest standing witness to that
///         defect anywhere in the suite.
///     </para>
///     <para>
///         EF's <c>AdHocMiscellaneousQuerySqliteTest</c> also overrides <c>Average_with_cast</c>
///         and <c>Check_inlined_constants_redacting</c>. <b>Neither is taken</b>: both passed
///         unmodified when measured, and an override adopted in advance of a measurement is a
///         workaround for a limitation this arrangement may not have.
///     </para>
/// </remarks>
public class AdHocMiscellaneousQuerySqliteInfoCarrierTest(NonSharedFixture fixture)
    : AdHocMiscellaneousQueryRelationalTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.Sqlite);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     A no-op. EF's SQLite writes
    ///     <c>new SqliteDbContextOptionsBuilder(o).UseParameterizedCollectionMode(…)</c>, a
    ///     relational option on the client's builder that this provider does not have. No test in
    ///     this base asks for a non-default mode, so nothing here depends on it — measured, not
    ///     assumed.
    /// </remarks>
    protected override DbContextOptionsBuilder SetParameterizedCollectionMode(
        DbContextOptionsBuilder optionsBuilder,
        ParameterTranslationMode parameterizedCollectionMode)
        => optionsBuilder;

    /// <inheritdoc />
    /// <remarks>EF's own <c>AdHocMiscellaneousQuerySqliteTest</c>'s, verbatim.</remarks>
    protected override Task Seed2951(Context2951 context)
        => context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE ZeroKey (Id int);
            INSERT INTO ZeroKey VALUES (NULL)
            """);

    /// <inheritdoc />
    /// <remarks>
    ///     EF's <c>AdHocMiscellaneousQueryInMemoryTest</c>'s: it asserts the count of EF's
    ///     relational command cache, which the <em>client</em> here does not have.
    /// </remarks>
    public override Task Explicitly_compiled_query_does_not_add_cache_entry()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's InMemory class's, for the same reason.</remarks>
    public override Task Inlined_dbcontext_is_not_leaking()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's InMemory class's, for the same reason.</remarks>
    public override Task Relational_command_cache_creates_new_entry_when_parameter_nullability_changes()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's InMemory class's, for the same reason.</remarks>
    public override Task Variable_from_closure_is_parametrized()
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
        _harness.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }
}
