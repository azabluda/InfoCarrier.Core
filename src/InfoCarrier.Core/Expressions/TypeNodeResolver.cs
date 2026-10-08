// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection.Metadata;
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
    private readonly Dictionary<Type, TypeNode> _responseShapes = [];
    private readonly Dictionary<string, AnonymousShapeCatalog.Entry> _catalogEntries = new(StringComparer.Ordinal);
    private TypeNodeMapper? _shapeMapper;
    internal TypeNodeMapper? ShapeMapper
    {
        get => _shapeMapper;
        set
        {
            _shapeMapper = value;
            foreach ((Type type, TypeNode node) in _responseShapes)
            {
                value?.BindResponseShape(type, node);
            }
        }
    }
    private AnonymousShapeCatalog? _shapeCatalog;
    internal void RequireServerShapeCatalog() => _shapeCatalog ??= AnonymousShapeCatalog.Empty;

    /// <summary>
    ///     Uses an immutable catalog constructed by trusted server configuration. Attaching a
    ///     catalog disables client exchange-local original-type restoration on this resolver.
    ///     Component permissions are still checked separately on every resolution.
    /// </summary>
    public virtual void UseAnonymousShapeCatalog(AnonymousShapeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Model is not null && !ReferenceEquals(catalog.Model, _model))
        {
            throw new InvalidOperationException("The anonymous-shape catalog belongs to another server model.");
        }

        _shapeCatalog = catalog;
        _catalogEntries.Clear();
    }
    private readonly HashSet<string> _shapes = new(StringComparer.Ordinal);
    private static readonly TypeNameParseOptions NameParseOptions = new() { MaxNodes = 1024 };
    internal void ResetShapes()
    {
        _shapes.Clear();
        _responseShapes.Clear();
        _catalogEntries.Clear();
    }

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
        TypeNodeComplexity.Validate(node);
        ValidateShapes(node, 0);
        if (HasShape(node))
        {
            Type shaped = ResolveCore(node);
            if (!_effective.IsAllowed(shaped))
            {
                throw new InvalidOperationException(BuildRejection(shaped));
            }

            return shaped;
        }

        string cacheKey = node.CacheIdentity();

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
        if (ContainsAnonymous(resolved) || !_effective.IsAllowed(resolved))
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
        if (node.ShapeMembers is not null)
        {
            string key = node.CacheIdentity();
            AnonymousShapeCatalog.Entry? entry = null;
            if (_shapeCatalog is not null && !_catalogEntries.TryGetValue(key, out entry))
            {
                entry = _shapeCatalog.Require(node);
                // ValidateShapes already bounded all anonymous identities for this exchange.
                _catalogEntries.Add(key, entry);
            }
            Type[] components = node.GenericArguments.Select(Resolve).ToArray();
            if (_shapeCatalog is null && ShapeMapper?.TryOriginalShape(node, out Type? original) == true)
            {
                return original!;
            }

            Type generated = entry is not null ? AnonymousShapeCatalog.Resolve(entry, components)
                : throw new InvalidOperationException("Anonymous-shape generation requires a trusted server catalog.");
            if (_responseShapes.TryGetValue(generated, out TypeNode? previous)
                && previous.CacheIdentity() != node.CacheIdentity())
            {
                throw new InvalidOperationException("Different anonymous type identities for the same registered structure cannot share one exchange.");
            }

            TypeNode saved = AnonymousShapeTypes.Snapshot(node);
            _responseShapes[generated] = saved;
            ShapeMapper?.BindResponseShape(generated, saved);
            return generated;
        }

        if (node.ArrayElement is not null)
        {
            Type element = Resolve(node.ArrayElement);
            return node.ArrayRank == 1 ? element.MakeArrayType() : element.MakeArrayType(node.ArrayRank);
        }

        // Generic reconstruction.
        if (node.GenericArguments.Count > 0)
        {
            Type definition = ResolveByName(node.Name)
                ?? throw new InvalidOperationException($"Cannot resolve generic type definition '{node.Name}'.");
            RejectRawAnonymous(definition);
            Type[] arguments = node.GenericArguments.Select(ResolveCore).ToArray();
            return definition.MakeGenericType(arguments);
        }

        // Plain type by full name.
        Type? resolved = ResolveByName(node.Name);
        if (resolved is not null)
        {
            RejectRawAnonymous(resolved);
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

    private static bool HasShape(TypeNode node)
        => node.ShapeMembers is not null || node.ArrayElement is not null || node.GenericArguments.Any(HasShape);

    private static bool ContainsAnonymous(Type type)
        => AnonymousShapeTypes.IsOriginal(type) || AnonymousShapeTypes.IsGenerated(type)
            || (type.IsArray && ContainsAnonymous(type.GetElementType()!))
            || (type.IsGenericType && type.GetGenericArguments().Any(ContainsAnonymous));

    private static void RejectRawAnonymous(Type type)
    {
        if (ContainsAnonymous(type))
        {
            throw new InvalidOperationException("Anonymous data requires a bounded shape descriptor, not a CLR name.");
        }
    }

    private void ValidateShapes(TypeNode node, int depth)
    {
        if (node is null || string.IsNullOrEmpty(node.Name) || node.GenericArguments is null)
        {
            throw new InvalidOperationException("A type descriptor requires a name and component list.");
        }

        if (depth > AnonymousShapeTypes.MaximumDepth)
        {
            throw new InvalidOperationException("The type descriptor depth budget is exhausted.");
        }

        if (node.ShapeMembers is not null)
        {
            AnonymousShapeTypes.Validate(node);
        }
        else if (node.Name.StartsWith(AnonymousShapeTypes.Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An anonymous-shape identity requires member metadata.");
        }
        else if (node.ArrayElement is null)
        {
            // FullName can contain constructed generic types, especially inside ordinary arrays.
            // Parse without loading assemblies or constructing runtime types. Raw names and
            // structured arguments must consume the same depth budget before either lookup path.
            if (node.Name.Length > 16384)
            {
                throw new InvalidOperationException("The type-name length budget is exhausted.");
            }

            TypeName name;
            try
            {
                name = TypeName.Parse(node.Name, NameParseOptions);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                throw new InvalidOperationException("Invalid or excessively complex CLR type name.", error);
            }

            ValidateNameDepth(name, depth);
            if (node.GenericArguments.Count > 0 && !name.IsSimple)
            {
                throw new InvalidOperationException("Structured generic arguments require a simple generic definition name.");
            }
        }

        if (node.ArrayElement is not null)
        {
            if (node.Name != "InfoCarrier.ShapeArray.v1" || node.ArrayRank is < 1 or > 32
                || node.GenericArguments.Count != 0 || node.EntityTypeName is not null)
            {
                throw new InvalidOperationException("Invalid anonymous-shape array descriptor.");
            }

            ValidateShapes(node.ArrayElement, depth + 1);
        }

        foreach (TypeNode component in node.GenericArguments)
        {
            ValidateShapes(component, depth + 1);
        }

        if (node.ShapeMembers is not null)
        {
            string identity = node.CacheIdentity();
            if (!_shapes.Contains(identity) && _shapes.Count >= AnonymousShapeTypes.MaximumShapes)
            {
                throw new InvalidOperationException("The exchange anonymous-shape budget is exhausted.");
            }

            _shapes.Add(identity);
        }
    }

    private static void ValidateNameDepth(TypeName name, int depth)
    {
        if (depth > AnonymousShapeTypes.MaximumDepth)
        {
            throw new InvalidOperationException("The type descriptor depth budget is exhausted.");
        }

        if (name.IsArray || name.IsPointer || name.IsByRef)
        {
            ValidateNameDepth(name.GetElementType(), depth + 1);
        }
        else if (name.IsConstructedGenericType)
        {
            foreach (TypeName argument in name.GetGenericArguments())
            {
                ValidateNameDepth(argument, depth + 1);
            }
        }
    }
}
