// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
///         <b>Four <c>Task.CompletedTask</c> overrides moved across unchanged, and they are not a
///         tier artefact.</b> They are EF's own <c>AdHocMiscellaneousQueryInMemoryTest</c>'s. Three
///         assert the size of EF's <b>relational command cache</b>, and it is the <em>client's</em>
///         cache they read, which this client never fills because it stops at
///         <c>IDatabase.CompileQuery</c> (ADR-006). <b>This said the client "is not a relational
///         provider on either tier" until 2026-09-15</b>, which R135 had made false; the reason
///         survives on ADR-006 instead. The fourth is not a cache test and does not skip any more;
///         it is InfoCarrier defect #113, and its own remark says what was measured.
///     </para>
///     <para>
///         <b>Until 2026-09-15 this paragraph called <c>Multiple_different_entity_type_from_different_namespaces</c>
///         a red left on purpose</b>, a witness to R71's discarded <c>FromSql</c> root. R75 made that
///         a refusal, the override below pins it, and the class has no red.
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
    private readonly NonSharedModelInfoCarrierHarness _harness = new(SqliteInfoCarrierTier.Instance);

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    /// <remarks>
    ///     A no-op. EF's SQLite writes
    ///     <c>new SqliteDbContextOptionsBuilder(o).UseParameterizedCollectionMode(…)</c>, a
    ///     relational option on the client's builder that this provider does not have, so the
    ///     server translates with its own mode. <b>This read "No test in this base asks for a
    ///     non-default mode … measured, not assumed" until 2026-09-15, and
    ///     <c>Check_inlined_constants_redacting</c> asks for <c>Constant</c>.</b> It passes because
    ///     it asserts no SQL: EF sends <c>IN (1, 2, 3)</c> and the server sends
    ///     <c>IN (@Value1, @Value2, @Value3)</c>. That is a legitimate deviation by the owner's rule
    ///     of the same day: no dangerous SQL runs, and what the caller sees is right.
    /// </remarks>
    protected override DbContextOptionsBuilder SetParameterizedCollectionMode(
        DbContextOptionsBuilder optionsBuilder,
        ParameterTranslationMode parameterizedCollectionMode)
        => optionsBuilder;

    /// <inheritdoc />
    /// <remarks>
    ///     <b>EF issue #23981 is about entity types in different namespaces; the
    ///     <c>FromSqlRaw</c> is only how the base reaches them.</b> This provider refuses
    ///     <c>FromSql</c> (R75), so the scenario cannot be reached here at all, and the refusal is
    ///     what is pinned. Until R75 this failed with a <c>NullReferenceException</c> out of this
    ///     provider's own materializer — the discarded query root surfacing three layers from its
    ///     cause, which is why it read as an unrelated defect.
    /// </remarks>
    [InfoCarrierDesign(
        Decisions.SecurityReview,
        Decisions.RawSqlGrant,
        Justification = "Raw SQL is refused unless the server grants it, and this fixture does not.")]
    public override Task Multiple_different_entity_type_from_different_namespaces(bool async)
        => FromSqlAssertions.NotSupportedAsync(
            () => base.Multiple_different_entity_type_from_different_namespaces(async));

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
    ///     relational command cache, which the <em>client</em> here does not have. Measured
    ///     2026-09-15 without the skip: the count is 0 where the base expects 1.
    /// </remarks>
    [InfoCarrierDesign(
        6,
        Justification = CommandCacheIsTheServers,
        Repository = UpstreamRepository.EfCore,
        UpstreamPath = "test/EFCore.InMemory.FunctionalTests/Query/AdHocMiscellaneousQueryInMemoryTest.cs",
        UpstreamFirstLine = 11,
        UpstreamLastLine = 12,
        Skip = true)]
    public override Task Explicitly_compiled_query_does_not_add_cache_entry()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>Not a command-cache test, whatever this remark said before 2026-09-15.</b> EF refuses
    ///         a client projection that calls an instance method of the <c>DbContext</c>, because the
    ///         cache would hold that context, and the base asserts the refusal. This client does not
    ///         refuse, so the base's own <c>Assert.Throws</c> finds nothing thrown, and that is what is
    ///         asserted.
    ///     </para>
    ///     <para>
    ///         <b>An InfoCarrier defect, #113.</b> Measured 2026-09-15 with contexts that are not
    ///         pooled: a context that ran this query stays reachable after it is disposed, and only
    ///         compacting EF's memory cache releases it, while a context that ran a plain query is
    ///         collected. The answer is right; the memory is not released. Until the same day this
    ///         override skipped, labelled DESIGN with the claim that no cache held a context, which
    ///         the measurement disproved.
    ///     </para>
    /// </remarks>
    [InfoCarrierDefect(113)]
    public override async Task Inlined_dbcontext_is_not_leaking()
    {
        var failure = await Assert.ThrowsAsync<Xunit.Sdk.ThrowsException>(base.Inlined_dbcontext_is_not_leaking);

        Assert.Contains("No exception was thrown", failure.Message, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    /// <remarks>EF's InMemory class's, for the same reason as the first. Measured: 1 where the base expects 2.</remarks>
    [InfoCarrierDesign(
        6,
        Justification = CommandCacheIsTheServers,
        Repository = UpstreamRepository.EfCore,
        UpstreamPath = "test/EFCore.InMemory.FunctionalTests/Query/AdHocMiscellaneousQueryInMemoryTest.cs",
        UpstreamFirstLine = 17,
        UpstreamLastLine = 18,
        Skip = true)]
    public override Task Relational_command_cache_creates_new_entry_when_parameter_nullability_changes()
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>EF's InMemory class's, for the same reason as the first. Measured: 1 where the base expects 2.</remarks>
    [InfoCarrierDesign(
        6,
        Justification = CommandCacheIsTheServers,
        Repository = UpstreamRepository.EfCore,
        UpstreamPath = "test/EFCore.InMemory.FunctionalTests/Query/AdHocMiscellaneousQueryInMemoryTest.cs",
        UpstreamFirstLine = 20,
        UpstreamLastLine = 21,
        Skip = true)]
    public override Task Variable_from_closure_is_parametrized()
        => Task.CompletedTask;

    private const string CommandCacheIsTheServers =
        "The client captures the query at IDatabase.CompileQuery and never compiles past it, so EF's relational "
        + "command cache, which these tests count, belongs to the server's provider.";

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
