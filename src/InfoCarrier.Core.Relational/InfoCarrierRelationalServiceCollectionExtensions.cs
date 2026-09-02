// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InfoCarrier.Core.Relational;

/// <summary>
///     Registers the relational half of InfoCarrier (#97).
/// </summary>
public static class InfoCarrierRelationalServiceCollectionExtensions
{
    /// <summary>
    ///     Tells this half that the backing store is a relational database.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Call it on both halves.</b> The client needs it to recognise a captured raw-SQL
    ///         query root and carry it instead of refusing it; the server needs it to rebuild one.
    ///         A registration on one side only fails asymmetrically, which is the hazard ADR-012
    ///         states in CLR-type terms to avoid.
    ///     </para>
    ///     <para>
    ///         <b>It grants nothing.</b> Whether a payload may carry SQL is still
    ///         <c>AddInfoCarrierArbitrarySqlExecution()</c> on the server and
    ///         <c>AllowArbitrarySqlExecution()</c> on the client, and both still default to
    ///         refusing. This call only supplies the knowledge of what a relational query root
    ///         <em>is</em>. Read <c>docs/security-review.md</c> §5a before granting the other.
    ///     </para>
    ///     <para>
    ///         <b>Order does not matter.</b> The core package registers its no-op default with
    ///         <c>TryAdd</c>, so calling this first leaves it stood down and calling it second
    ///         replaces it.
    ///     </para>
    /// </remarks>
    /// <param name="services">The collection to register into.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddInfoCarrierRelational(this IServiceCollection services)
    {
        services.RemoveAll<IInfoCarrierRelationalQueryRoots>();

        return services.AddSingleton<IInfoCarrierRelationalQueryRoots, InfoCarrierRelationalQueryRoots>();
    }

    /// <summary>
    ///     The extra registration a <em>client</em> needs, on top of
    ///     <see cref="AddInfoCarrierRelational" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>THE CLIENT HALF ONLY, and putting it in the shared call is a real bug rather
    ///         than untidiness.</b> A server is an ordinary EF application against a relational
    ///         provider, so it already has EF's own
    ///         <c>RelationalDatabaseFacadeDependencies</c> — one that owns a live connection.
    ///         Replacing that with <see cref="InfoCarrierRelationalFacadeDependencies" />, whose
    ///         <c>RelationalConnection</c> throws, would break the server's own database access.
    ///         The two halves need different things from this package and the API says so.
    ///     </para>
    ///     <para>
    ///         What it buys is <c>Database.SqlQuery&lt;T&gt;</c>:
    ///         <c>RelationalDatabaseFacadeExtensions</c> opens with a CLR type test against
    ///         <c>IRelationalDatabaseFacadeDependencies</c> that runs before any expression is
    ///         built, and only an object that really implements the interface can satisfy it.
    ///     </para>
    /// </remarks>
    /// <param name="services">The client's collection.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    public static IServiceCollection AddInfoCarrierRelationalClient(this IServiceCollection services)
    {
        AddInfoCarrierRelational(services);

        // Under both interfaces, and forwarding rather than constructing twice, because EF's own
        // `EntityFrameworkRelationalServicesBuilder` does exactly that:
        // `TryAdd<IDatabaseFacadeDependencies>(p => p.GetRequiredService<IRelationalDatabaseFacadeDependencies>())`.
        // `RelationalDatabaseFacadeExtensions.GetFacadeDependencies` satisfies its type test only
        // if the two resolve to the SAME object.
        services.RemoveAll<IDatabaseFacadeDependencies>();
        services.AddScoped<IRelationalDatabaseFacadeDependencies, InfoCarrierRelationalFacadeDependencies>();

        return services.AddScoped<IDatabaseFacadeDependencies>(
            p => p.GetRequiredService<IRelationalDatabaseFacadeDependencies>());
    }
}
