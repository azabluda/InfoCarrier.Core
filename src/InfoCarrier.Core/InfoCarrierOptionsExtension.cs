// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.ValueMapping;
using System.Runtime.CompilerServices;

namespace InfoCarrier.Core;

/// <summary>
///     The EF Core options extension that carries the client's <see cref="IInfoCarrierClient" />
///     and registers InfoCarrier provider services (DI-first, requirements §4.2). Added by
///     <see cref="InfoCarrierDbContextOptionsBuilderExtensions.UseInfoCarrier(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder, IInfoCarrierClient, System.Action{InfoCarrierClientOptions})" />.
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
    ///     <see cref="InfoCarrierDeploymentBuilderExtensions.AddAllowedTypes" />.
    /// </summary>
    public virtual IReadOnlyList<Type> AllowedTypes { get; private set; } = [];

    internal AnonymousShapeSet RegisteredAnonymousShapes { get; private set; } = AnonymousShapeSet.Empty;

    internal IReadOnlyList<IInfoCarrierValueMapper> ValueMappers { get; private set; } = [];

    internal InfoCarrierOptionsExtension WithDeployment(InfoCarrierDeploymentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var clone = (InfoCarrierOptionsExtension)MemberwiseClone();
        clone._info = null;
        clone.RegisteredAnonymousShapes = RegisteredAnonymousShapes.Add(snapshot.AnonymousShapeTypes);
        clone.AllowedTypes = [.. AllowedTypes, .. snapshot.AllowedTypes];
        clone.ValueMappers = [.. ValueMappers, .. snapshot.ValueMappers];
        clone.ArbitrarySqlExecutionAllowed |= snapshot.AllowArbitrarySqlExecution;
        return clone;
    }

    /// <summary>
    ///     Whether this client may send a query carrying raw SQL (#60). <c>false</c> unless the
    ///     application called
    ///     <see cref="InfoCarrierDeploymentBuilderExtensions.AllowArbitrarySqlExecution" />.
    /// </summary>
    public virtual bool ArbitrarySqlExecutionAllowed { get; private set; }

    /// <summary>
    ///     Whether the server's backing store is relational. <c>true</c> unless the application
    ///     called <see cref="InfoCarrierClientOptions.ServerUsesRelationalQueryRules" />.
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

    internal static AnonymousShapeSet RegisteredAnonymousShapesFor(Microsoft.EntityFrameworkCore.DbContext context)
        => Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<IDbContextOptions>(context)
            .Extensions
            .OfType<InfoCarrierOptionsExtension>()
            .FirstOrDefault()
            ?.RegisteredAnonymousShapes ?? AnonymousShapeSet.Empty;

    /// <inheritdoc />
    public virtual DbContextOptionsExtensionInfo Info
        => _info ??= new ExtensionInfo(this);

    /// <inheritdoc />
    public virtual void ApplyServices(IServiceCollection services)
    {
        foreach (IInfoCarrierValueMapper mapper in ValueMappers)
        {
            services.AddSingleton<IInfoCarrierValueMapper>(mapper);
        }

        services.AddEntityFrameworkInfoCarrier();
    }

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
        private readonly InfoCarrierOptionsExtension _extension = extension;

        public override bool IsDatabaseProvider => true;

        public override string LogFragment => "using InfoCarrier ";

        public override int GetServiceProviderHashCode()
        {
            var hash = new HashCode();
            foreach (IInfoCarrierValueMapper mapper in _extension.ValueMappers)
            {
                hash.Add(RuntimeHelpers.GetHashCode(mapper));
            }

            return _extension.ValueMappers.Count == 0 ? 0 : hash.ToHashCode();
        }

        public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
        {
            debugInfo["InfoCarrier"] = "1";
            if (_extension.ValueMappers.Count > 0)
            {
                debugInfo["InfoCarrier:ValueMappers"] = GetServiceProviderHashCode().ToString();
            }
        }

        public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
            => other is ExtensionInfo otherInfo
                && _extension.ValueMappers.Count == otherInfo._extension.ValueMappers.Count
                && _extension.ValueMappers.Zip(otherInfo._extension.ValueMappers, ReferenceEquals).All(static same => same);
    }
}
