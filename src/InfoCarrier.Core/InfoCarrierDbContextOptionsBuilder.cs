// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     InfoCarrier-specific options, configured through
///     <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier(DbContextOptionsBuilder, IInfoCarrierClient, Action{InfoCarrierDbContextOptionsBuilder})" />.
/// </summary>
/// <remarks>
///     The shape every EF Core provider uses for its own options —
///     <c>UseSqlite(connection, o =&gt; o.CommandTimeout(30))</c> — so that a second option needs
///     no new overload of <c>UseInfoCarrier</c>.
/// </remarks>
/// <param name="optionsBuilder">The builder being configured.</param>
public class InfoCarrierDbContextOptionsBuilder(DbContextOptionsBuilder optionsBuilder)
{
    private readonly DbContextOptionsBuilder _optionsBuilder = optionsBuilder;

    /// <summary>
    ///     Permits a wire payload to name these CLR types, in addition to the ones the model
    ///     already implies (ADR-008 constraint 2).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>What this is for.</b> A query may legitimately name a class that is not part of
    ///         the model: most often the <c>EF.Functions</c> host of the <em>server's</em> provider.
    ///         <c>EF.Functions.Like</c> works everywhere because it is declared on EF Core's own
    ///         <c>DbFunctionsExtensions</c>, but <c>EF.Functions.Glob</c> is SQLite's,
    ///         <c>EF.Functions.DateDiffDay</c> is SQL Server's, and this package references
    ///         neither. Registering the host is how a caller says "my server can run these".
    ///     </para>
    ///     <para>
    ///         <b>Register the same types on the server, with
    ///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierAllowedTypes" />.</b>
    ///         This is ADR-012's rule for value mappers restated: a type admitted on one side only
    ///         is worse than one admitted on neither, because the client then ships a query the
    ///         server refuses to read. The client's list decides what may be <em>sent</em>; the
    ///         server's decides what may be <em>named by a payload</em>, and only the second is a
    ///         security boundary.
    ///     </para>
    ///     <para>
    ///         <b>Read <c>docs/security-review.md</c> §2 before registering anything.</b> Its
    ///         safety argument is a conjunction, and it is broken by admitting any of
    ///         <c>Binder</c>, <c>MethodBase</c>, <c>MethodInfo</c>, <c>ConstructorInfo</c>,
    ///         <c>PropertyInfo</c>, <c>Activator</c>, <c>Assembly</c> or <c>AppDomain</c> — none of
    ///         which looks dangerous alone. Nothing is admitted here by inference: this list is a
    ///         deliberate decision by the application, which is why the API exists rather than a
    ///         rule that guesses.
    ///     </para>
    /// </remarks>
    /// <param name="types">The types to admit.</param>
    /// <returns>The same builder, so calls chain.</returns>
    public virtual InfoCarrierDbContextOptionsBuilder AllowTypes(params Type[] types)
    {
        ArgumentNullException.ThrowIfNull(types);

        InfoCarrierOptionsExtension extension =
            (_optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>() ?? new InfoCarrierOptionsExtension())
                .WithAllowedTypes(types);

        ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }

    /// <summary>
    ///     Permits this client to send a query carrying raw SQL - <c>FromSql</c>,
    ///     <c>FromSqlRaw</c>, <c>FromSqlInterpolated</c> (#60).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The server decides whether it runs, and it refuses by default.</b> This half
    ///         governs what this application's own code may <em>send</em>; the grant is
    ///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />
    ///         on the server, and it is the security boundary. Registering here alone produces a
    ///         query the server refuses, exactly as <c>AllowTypes</c> does.
    ///     </para>
    ///     <para>
    ///         <b>Named for what it grants rather than for the API it unblocks.</b>
    ///         <c>Sqlite/RawSqlExecutionProbeTest</c> (R94) measured both halves: one
    ///         <c>CommandText</c> executes every statement it contains, and an uncomposed
    ///         <c>FromSqlRaw</c> reaches the store unwrapped. There is no read-only subset of this
    ///         to ask for. Read <c>docs/security-review.md</c> section 5a.
    ///     </para>
    ///     <para>
    ///         Without it, a <c>FromSql</c> query is refused with EF's own
    ///         <c>TranslationFailed</c> - the answer every other provider gives for a construct it
    ///         cannot translate.
    ///     </para>
    /// </remarks>
    /// <returns>The same builder, so calls chain.</returns>
    public virtual InfoCarrierDbContextOptionsBuilder AllowArbitrarySqlExecution()
    {
        InfoCarrierOptionsExtension extension =
            (_optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>() ?? new InfoCarrierOptionsExtension())
                .WithArbitrarySqlExecution();

        ((IDbContextOptionsBuilderInfrastructure)_optionsBuilder).AddOrUpdateExtension(extension);

        return this;
    }
}
