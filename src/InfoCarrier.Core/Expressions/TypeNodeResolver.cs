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
public class TypeNodeResolver
{
    private readonly IModel? _model;
    private readonly TypeAllowlist _allowlist;
    private readonly Dictionary<string, Type> _cache = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeNodeResolver" /> class.
    /// </summary>
    /// <param name="model">The EF model used to resolve entity types by model identity.</param>
    /// <param name="allowlist">
    ///     The types a payload may name (ADR-008 constraint 2). Defaults to one derived from
    ///     <paramref name="model" /> — the allowlist is <em>on by default</em>, never opt-in.
    /// </param>
    public TypeNodeResolver(IModel? model = null, TypeAllowlist? allowlist = null)
    {
        _model = model;
        _allowlist = allowlist ?? TypeAllowlist.ForModel(model);
    }

    /// <summary>
    ///     Resolves a type node to its CLR type.
    /// </summary>
    public virtual Type Resolve(TypeNode node)
    {
        string cacheKey = node.ToString();
        if (_cache.TryGetValue(cacheKey, out Type? cached))
        {
            return cached;
        }

        Type resolved = ResolveCore(node);

        // Enforced after resolution, not instead of it: the name has to be resolved to know
        // what it denotes, but nothing is constructed from it until it clears the allowlist.
        if (!_allowlist.IsAllowed(resolved))
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
            Type[] arguments = node.GenericArguments.Select(Resolve).ToArray();
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
