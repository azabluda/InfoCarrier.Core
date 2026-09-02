// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The client-side <see cref="IRelationalDatabaseFacadeDependencies" /> that lets
///     <c>Database.SqlQuery&lt;T&gt;</c> reach this provider (#56, option D).
/// </summary>
/// <remarks>
///     <para>
///         <b>The obstacle it removes is a type test, not a capability.</b>
///         <c>RelationalDatabaseFacadeExtensions.SqlQueryRaw</c> opens with
///         <c>dependencies is IRelationalDatabaseFacadeDependencies</c> and throws
///         <c>RelationalStrings.RelationalNotInUse</c> otherwise. That runs before any expression
///         is built, so nothing downstream — no wire node, no allowlist entry — can be reached
///         past it. A CLR type test is answered by the runtime type's interface table, so a name
///         or a shape cannot satisfy it; only an object that really implements the interface can.
///     </para>
///     <para>
///         <b><c>InfoCarrier.Core</c> still references nothing relational, and
///         <c>architecture.md</c> §6a D3 stands as written.</b> <see cref="DatabaseFacade" />
///         resolves its dependencies with
///         <c>context.GetService&lt;IDatabaseFacadeDependencies&gt;()</c>, so the registration is
///         replaceable from outside the package — and the harness is an application, exactly as it
///         is for R85's <c>AddInfoCarrierAllowedTypes</c> and R95's
///         <c>AddInfoCarrierArbitrarySqlExecution</c>. A shipped <c>InfoCarrier.Core.Relational</c>
///         package would hold this same class; that is a packaging step, not a design one.
///     </para>
///     <para>
///         <b>The three relational members throw, and nothing on this path calls them.</b>
///         <c>SqlQueryRaw</c> reads <see cref="QueryProvider" />,
///         <see cref="TypeMappingSource" /> and <see cref="AdHocMapper" />, all of which are on
///         the <em>core</em> interface. <see cref="RelationalConnection" />,
///         <see cref="RawSqlCommandBuilder" /> and <see cref="CommandLogger" /> have no meaning on
///         a client with no database. That shape is already proven here:
///         <see cref="RelationalInfoCarrierTestStore" /> refuses <c>Connection</c> the same way,
///         and ADR-013's amendment records why it is sound — the callers that want the connection
///         are not the callers that want the rest.
///     </para>
/// </remarks>
public sealed class InfoCarrierRelationalFacadeDependencies(
    IDbContextTransactionManager transactionManager,
    IDatabaseCreator databaseCreator,
    IExecutionStrategy executionStrategy,
    IExecutionStrategyFactory executionStrategyFactory,
    IEnumerable<IDatabaseProvider> databaseProviders,
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> commandLogger,
    IConcurrencyDetector concurrencyDetector,
    ICoreSingletonOptions coreOptions,
    IAsyncQueryProvider queryProvider,
    IAdHocMapper adHocMapper,
    ITypeMappingSource typeMappingSource) : IRelationalDatabaseFacadeDependencies
{
    /// <inheritdoc />
    public IDbContextTransactionManager TransactionManager { get; } = transactionManager;

    /// <inheritdoc />
    public IDatabaseCreator DatabaseCreator { get; } = databaseCreator;

    /// <inheritdoc />
    public IExecutionStrategy ExecutionStrategy { get; } = executionStrategy;

    /// <inheritdoc />
    public IExecutionStrategyFactory ExecutionStrategyFactory { get; } = executionStrategyFactory;

    /// <inheritdoc />
    public IEnumerable<IDatabaseProvider> DatabaseProviders { get; } = databaseProviders;

    /// <inheritdoc />
    public IConcurrencyDetector ConcurrencyDetector { get; } = concurrencyDetector;

    /// <inheritdoc />
    public ICoreSingletonOptions CoreOptions { get; } = coreOptions;

    /// <inheritdoc />
    public IAsyncQueryProvider QueryProvider { get; } = queryProvider;

    /// <inheritdoc />
    public IAdHocMapper AdHocMapper { get; } = adHocMapper;

    /// <inheritdoc />
    public ITypeMappingSource TypeMappingSource { get; } = typeMappingSource;

    /// <summary>
    ///     The relational command logger, which a client with no database does not have.
    /// </summary>
    /// <remarks>
    ///     Public here and the CORE logger explicit below, rather than the other way round,
    ///     because <see cref="IRelationalDatabaseFacadeDependencies" /> redeclares
    ///     <c>CommandLogger</c> with <c>new</c>: the name resolves to the relational one. EF's own
    ///     <c>RelationalDatabaseFacadeDependencies</c> is arranged this way for the same reason.
    /// </remarks>
    public IRelationalCommandDiagnosticsLogger CommandLogger
        => throw new InvalidOperationException(NoDatabase(nameof(CommandLogger)));

    /// <inheritdoc />
    IDiagnosticsLogger<DbLoggerCategory.Database.Command> IDatabaseFacadeDependencies.CommandLogger
        => commandLogger;

    /// <inheritdoc />
    public IRelationalConnection RelationalConnection
        => throw new InvalidOperationException(NoDatabase(nameof(RelationalConnection)));

    /// <inheritdoc />
    public IRawSqlCommandBuilder RawSqlCommandBuilder
        => throw new InvalidOperationException(NoDatabase(nameof(RawSqlCommandBuilder)));

    /// <summary>
    ///     Registers this as the client's facade dependencies, under both interfaces.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Under both, and forwarding rather than constructing twice, because EF's own
    ///         <c>EntityFrameworkRelationalServicesBuilder</c> does exactly that:
    ///         <c>TryAdd&lt;IDatabaseFacadeDependencies&gt;(p =&gt; p.GetRequiredService&lt;IRelationalDatabaseFacadeDependencies&gt;())</c>.
    ///         The type test is satisfied only if the two resolve to the same object.
    ///     </para>
    ///     <para>
    ///         <c>RemoveAll</c> first, so the order of this call and
    ///         <c>AddEntityFrameworkInfoCarrier</c> does not matter: EF registers the core
    ///         implementation with <c>TryAdd</c>, which stands down if ours is already there, and
    ///         <c>RemoveAll</c> takes it out if it is not.
    ///     </para>
    /// </remarks>
    public static IServiceCollection AddInfoCarrierRelationalFacade(IServiceCollection services)
    {
        services.RemoveAll<IDatabaseFacadeDependencies>();

        return services
            .AddScoped<IRelationalDatabaseFacadeDependencies, InfoCarrierRelationalFacadeDependencies>()
            .AddScoped<IDatabaseFacadeDependencies>(p => p.GetRequiredService<IRelationalDatabaseFacadeDependencies>());
    }

    private static string NoDatabase(string member)
        => $"The InfoCarrier client has no database of its own, so '{member}' has no value here. "
            + "A caller reaching for it wants the SERVER's, which does not cross the wire.";
}
