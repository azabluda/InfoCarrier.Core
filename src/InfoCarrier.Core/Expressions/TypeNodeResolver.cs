// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Resolves wire <see cref="TypeNode" /> identities back to CLR <see cref="Type" />s.
///     DI-resolved (no statics — rlinq's <c>TypeResolver.Instance</c> is the anti-pattern).
/// </summary>
/// <remarks>
///     Resolution order: (1) core library types by full name across loaded assemblies,
///     (2) generic reconstruction from the generic-type-definition + arguments,
///     (3) EF entity types via the model using <see cref="TypeNode.EntityTypeName" />
///     (research-findings §3/§7 — entities resolve through model identity, never shape).
/// </remarks>
/// <remarks>
///     Initializes a new instance of the <see cref="TypeNodeResolver" /> class.
/// </remarks>
/// <param name="model">The EF model used to resolve entity types by model identity.</param>
/// <param name="allowlist">
///     The types a payload may name (ADR-008 constraint 2). Defaults to one derived from
///     <paramref name="model" /> — the allowlist is <em>on by default</em>, never opt-in.
/// </param>
public class TypeNodeResolver(IModel? model = null, TypeAllowlist? allowlist = null)
{
    private readonly IModel? _model = model;
    private readonly TypeAllowlist _allowlist = allowlist ?? TypeAllowlist.ForModel(model);
    private readonly Dictionary<string, Type> _cache = new(StringComparer.Ordinal);

    // The list actually consulted: the DI-scoped one until an execution declares more, then a
    // widened copy of it. WIDENED RATHER THAN CONSULTED BESIDE, so the allowlist's own generic
    // decomposition can see the added types -- see `TypeAllowlist.With`.
    private TypeAllowlist _effective = allowlist ?? TypeAllowlist.ForModel(model);

    /// <summary>
    ///     Widens what this resolver admits for the duration of ONE execution, with the types the
    ///     executing context's own options declare.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The allowlist this object was built with cannot carry them, and that is a
    ///         carrier problem rather than an oversight.</b> This service is DI-scoped, so its
    ///         allowlist is <c>TypeAllowlist.ForModel(model)</c> and knows only what the model
    ///         implies. The application's own registrations
    ///         (<see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" />) travel on the
    ///         <em>options</em>, and <c>InfoCarrierOptionsExtension.AllowedTypesFor</c> says why
    ///         they must be read per execution and never captured: what <c>CompileQuery</c> returns
    ///         is cached across every context sharing an options shape.
    ///     </para>
    ///     <para>
    ///         <b>So the boundary and the materializer read the same fact off different carriers,
    ///         which is R120's shape exactly</b>, and it was silent for the same reason: the
    ///         difference only shows when a declared type comes BACK. A <c>DbParameter</c> — the
    ///         only registered type this suite had before — is sent and never returned, so the two
    ///         readers never disagreed out loud until <c>Database.SqlQuery&lt;T&gt;</c> returned
    ///         rows of a declared DTO. <c>QueryExecutor</c> is now the one reader for both.
    ///     </para>
    ///     <para>
    ///         <b>It widens and never narrows.</b> Everything the model implies stays admitted;
    ///         this only adds what the application declared.
    ///     </para>
    /// </remarks>
    /// <param name="types">The executing context's declared types.</param>
    public virtual void UseExecutionAllowedTypes(IReadOnlyList<Type> types)
        => _effective = _allowlist.With(types ?? []);

    /// <summary>
    ///     Resolves a type node to its CLR type.
    /// </summary>
    public virtual Type Resolve(TypeNode node)
    {
        string cacheKey = node.ToString();

        // THE CACHE MEMOIZES THE RESOLUTION AND NEVER THE PERMISSION, and the two were one lookup
        // until `UseExecutionAllowedTypes` existed. A name resolved while one execution's declared
        // types were in force must not stay admitted for the next execution, whose context may
        // declare nothing. So a hit still falls through to the allowlist check below.
        if (!_cache.TryGetValue(cacheKey, out Type? resolved))
        {
            resolved = ResolveCore(node);
        }

        // A generic argument is part of a name, not a payload of its own — nothing is ever
        // constructed from one — so it is judged as part of the type it appears in rather than
        // alone. `GraphUpdatesTestBase<TFixture>+Root` names the *fixture* as its argument, and
        // demanding the fixture clear the list separately rejected every model type nested in a
        // generic test base. The constructed type below still has to clear it, and an argument
        // that is not part of an allowed whole is still denied there.

        // Enforced after resolution, not instead of it: the name has to be resolved to know
        // what it denotes, but nothing is constructed from it until it clears the allowlist.
        if (!_effective.IsAllowed(resolved))
        {
            throw new InvalidOperationException(BuildRejection(resolved));
        }

        _cache[cacheKey] = resolved;
        return resolved;
    }

    /// <summary>
    ///     Explains a rejection in the terms that actually apply, since the two causes need
    ///     opposite responses: a client-only projection type means the query must be split, an
    ///     unrelated type means the payload is asking for something it should not have.
    /// </summary>
    private static string BuildRejection(Type type)
        => IsCompilerGenerated(type)
            ? $"Type '{type}' is a compiler-generated projection type and is not resolvable across "
                + "the wire: it exists only in the client's assembly, so no server could construct "
                + "it. The projection must be evaluated on the client (requirements §3, milestone M2)."
            : $"Type '{type}' is not on the deserialization allowlist (ADR-008 constraint 2). "
                + "Model entity types and their property types are allowed automatically; register "
                + "any additional projection type explicitly.";

    private static bool IsCompilerGenerated(Type type)
        => type.Name.Contains("AnonymousType", StringComparison.Ordinal)
            || type.Name.StartsWith("<>", StringComparison.Ordinal)
            || type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false);

    private Type ResolveCore(TypeNode node)
    {
        // Generic reconstruction.
        if (node.GenericArguments.Count > 0)
        {
            Type definition = ResolveByName(node.Name)
                ?? throw new InvalidOperationException($"Cannot resolve generic type definition '{node.Name}'.");
            Type[] arguments = node.GenericArguments.Select(ResolveCore).ToArray();
            return definition.MakeGenericType(arguments);
        }

        // Plain type by full name.
        Type? resolved = ResolveByName(node.Name);
        if (resolved is not null)
        {
            return resolved;
        }

        // EF entity by model identity (shared-type entities resolve by name).
        if (_model is not null)
        {
            Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType = node.EntityTypeName is not null
                ? _model.FindEntityType(node.EntityTypeName)
                : null;
            if (entityType is not null)
            {
                return entityType.ClrType;
            }
        }

        throw new InvalidOperationException($"Cannot resolve type '{node}'.");
    }

    private static Type? ResolveByName(string fullName)
    {
        // Fast path: mscorlib / current AppDomain types via Type.GetType without assembly.
        Type? type = Type.GetType(fullName, throwOnError: false);
        if (type is not null)
        {
            return type;
        }

        foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(fullName, throwOnError: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }
}
