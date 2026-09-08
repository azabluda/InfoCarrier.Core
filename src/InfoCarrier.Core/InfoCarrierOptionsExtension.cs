// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core;

/// <summary>
///     The EF Core options extension that carries the client's <see cref="IInfoCarrierClient" />
///     and registers InfoCarrier provider services (DI-first, requirements §4.2). Added by
///     <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, IInfoCarrierClient, System.Action{InfoCarrierDbContextOptionsBuilder})" />.
/// </summary>
public class InfoCarrierOptionsExtension : IDbContextOptionsExtension
{
    private DbContextOptionsExtensionInfo? _info;

    /// <summary>
    ///     The client used to ship operations to the server. Resolved from options (v1 pattern)
    ///     so the provider can be constructed per-context.
    /// </summary>
    public virtual IInfoCarrierClient? InfoCarrierClient { get; private set; }

    /// <summary>
    ///     Configures the client for this options instance.
    /// </summary>
    public virtual InfoCarrierOptionsExtension WithInfoCarrierClient(IInfoCarrierClient client)
    {
        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.InfoCarrierClient = client;
        return clone;
    }

    /// <summary>
    ///     The CLR types this client may name in a query beyond the ones its model implies
    ///     (ADR-008 constraint 2). Empty unless the application registered some — see
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" />.
    /// </summary>
    public virtual IReadOnlyList<Type> AllowedTypes { get; private set; } = [];

    /// <summary>
    ///     Adds to <see cref="AllowedTypes" /> for this options instance.
    /// </summary>
    /// <remarks>
    ///     Additive rather than replacing, so two calls to <c>AllowTypes</c> both count. Every
    ///     other <c>With…</c> on an EF options extension replaces, but this one names a set and a
    ///     caller configuring options in two places would silently lose the first list.
    /// </remarks>
    public virtual InfoCarrierOptionsExtension WithAllowedTypes(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.AllowedTypes = [.. AllowedTypes, .. types];
        return clone;
    }

    /// <summary>
    ///     Whether this client may send a query carrying raw SQL (#60). <c>false</c> unless the
    ///     application called
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />.
    /// </summary>
    public virtual bool ArbitrarySqlExecutionAllowed { get; private set; }

    /// <summary>
    ///     Sets <see cref="ArbitrarySqlExecutionAllowed" /> for this options instance.
    /// </summary>
    public virtual InfoCarrierOptionsExtension WithArbitrarySqlExecution()
    {
        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.ArbitrarySqlExecutionAllowed = true;
        return clone;
    }


    /// <summary>
    ///     Whether the server's backing store is relational. <c>true</c> unless the application
    ///     called <see cref="InfoCarrierDbContextOptionsBuilder.UseNonRelationalServerStore" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>KNOWLEDGE, NOT PERMISSION, and the distinction is R120's.</b> Nothing here is
    ///         granted or withheld: this states a fact about the deployment that the client cannot
    ///         work out for itself. The client has no database and never sees the server's
    ///         provider, so it cannot tell a relational store from a document one, and some rules
    ///         are only true of the first.
    ///     </para>
    ///     <para>
    ///         <b>It guards four things, and each of the first three was made conditional because
    ///         enforcing it over EF's InMemory provider failed a query that store can answer.</b>
    ///         Every relational provider refuses all three, so refusing them by default keeps LINQ
    ///         written against this provider portable.
    ///     </para>
    ///     <list type="number">
    ///         <item>
    ///             <description>
    ///                 A <c>Distinct</c> or set operation over a projection that carries a
    ///                 collection, whose identifying columns do not survive it. Measured at eight
    ///                 specification tests.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 An ordering key of a type the wire cannot carry, which would otherwise be
    ///                 answered by fetching the whole table and sorting it here (R160, R164).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 A coalesce over a freshly constructed object in a row-deciding position,
    ///                 which no store translates and which does nothing (R165).
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 The wording of a refused <c>ExecuteUpdate</c> or <c>ExecuteDelete</c>: EF
    ///                 raises <c>NonQueryTranslationFailedWithDetails</c> for a bulk operation and
    ///                 the query form otherwise, and the spec suite asserts the difference (R171).
    ///             </description>
    ///         </item>
    ///     </list>
    ///     <para>
    ///         <b>The default is the relational one on purpose.</b> A server whose store is not
    ///         relational is the rare deployment, and the default that costs a wrong answer must
    ///         be the one you have to ask for.
    ///     </para>
    /// </remarks>
    public virtual bool ServerStoreIsRelational { get; private set; } = true;

    /// <summary>
    ///     Clears <see cref="ServerStoreIsRelational" /> for this options instance.
    /// </summary>
    public virtual InfoCarrierOptionsExtension WithNonRelationalServerStore()
    {
        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone.ServerStoreIsRelational = false;
        return clone;
    }

    /// <summary>
    ///     Whether the context's own options say the server's store is relational, read
    ///     <em>per execution</em> for the reason <see cref="AllowedTypesFor" /> records.
    /// </summary>
    internal static bool ServerStoreIsRelationalFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.ServerStoreIsRelational ?? true;

    /// <summary>
    ///     Whether the context's own options permit sending raw SQL, read <em>per execution</em>
    ///     for the reason <see cref="AllowedTypesFor" /> records.
    /// </summary>
    internal static bool ArbitrarySqlExecutionAllowedFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.ArbitrarySqlExecutionAllowed ?? false;

    /// <summary>
    ///     The types the context's own options admit, read <em>per execution</em>.
    /// </summary>
    /// <remarks>
    ///     Per execution and not captured, for the reason <c>InfoCarrierDatabase.ClientFor</c>
    ///     records: what <c>CompileQuery</c> returns is cached across every context sharing an
    ///     options shape, so anything per-context has to be resolved when the query runs.
    /// </remarks>
    internal static IReadOnlyList<Type> AllowedTypesFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.AllowedTypes ?? [];

    /// <inheritdoc />
    public virtual DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public virtual void ApplyServices(IServiceCollection services)
        => services.AddEntityFrameworkInfoCarrier();

    /// <inheritdoc />
    public virtual void Validate(IDbContextOptions options)
    {
        if (InfoCarrierClient is null)
        {
            throw new InvalidOperationException(
                "InfoCarrier requires an IInfoCarrierClient. Call UseInfoCarrier(client).");
        }
    }

    private sealed class ExtensionInfo(InfoCarrierOptionsExtension extension) : DbContextOptionsExtensionInfo(extension)
    {
        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "using InfoCarrier ";

        public override int GetServiceProviderHashCode() => 0;

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
            => debugInfo["InfoCarrier"] = "1";

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo;
    }
}
