// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Decides which CLR types a wire payload is permitted to name (ADR-008 constraint 2,
///     "strict allowlists ON by default").
/// </summary>
/// <remarks>
///     <para>
///         Without this, <see cref="TypeNodeResolver" /> resolved any name a payload supplied by
///         scanning every loaded assembly — a remote-code-execution vector the moment a network
///         transport exists, since the deserializer will then construct whatever it is told to.
///     </para>
///     <para>
///         It is also what makes the projection boundary <em>visible</em>. The in-process test
///         transport runs client and server in one <c>AppDomain</c>, so an assembly scan happily
///         found anonymous types and client-only DTOs and the server materialized them — which
///         no network transport could ever do. Denying them turns a silent illusion into a
///         legible failure (requirements §3, milestone M2).
///     </para>
/// </remarks>
public sealed class TypeAllowlist
{
    private static readonly HashSet<Type> BuiltInTypes =
    [
        typeof(object), typeof(void), typeof(string), typeof(decimal), typeof(Guid),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan),
        typeof(bool), typeof(byte), typeof(sbyte), typeof(char), typeof(double), typeof(float),
        typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(short), typeof(ushort),
        typeof(Uri), typeof(Version), typeof(Type), typeof(Enum), typeof(Array),
        typeof(IFormatProvider), typeof(IFormattable), typeof(IComparable),
        typeof(System.Globalization.CultureInfo), typeof(StringComparison),
        typeof(IEnumerable), typeof(ICollection), typeof(IList), typeof(IQueryable),
        typeof(IOrderedQueryable), typeof(IQueryProvider),

        // `ExecuteUpdate`'s setters travel as `IReadOnlyList<ITuple>` — EF's own signature for
        // the private overload the public one rewrites into
        // (`EntityFrameworkQueryableExtensions.ExecuteUpdateMethodInfo`). `IReadOnlyList<>` and
        // `Tuple<,>` were both already admitted and `ITuple` was the one name missing, which
        // refused the whole call and left it to be *evaluated* on the client — where that
        // overload is a marker that throws `UnreachableException: Can't call this overload
        // directly`. An interface constructs nothing, so admitting it widens nothing.
        typeof(System.Runtime.CompilerServices.ITuple),
    ];

    // Static holders that appear as a MethodNode's declaring type. A method still has to be
    // resolved by signature; this only permits the type to be named at all.
    private static readonly HashSet<Type> BuiltInOperationHosts =
    [
        typeof(Queryable), typeof(Enumerable), typeof(EF), typeof(DbFunctions),
        typeof(EntityFrameworkQueryableExtensions), typeof(Math), typeof(MathF),
        typeof(Convert), typeof(StringComparer), typeof(Expression),
        typeof(DbFunctionsExtensions),

        // `Regex`, admitted 2026-08-17 (M9 J20), reversing A46. EF's own SQLite provider
        // translates `Regex.IsMatch` to `REGEXP` and its InMemory provider evaluates it, so the
        // refusal was this provider disagreeing with every reference implementation.
        //
        // WHY THIS DOES NOT BREAK `security-review.md` §2's CONJUNCTION. That bound is over the
        // *reflection invocation surface* — `Binder`, `MethodBase`, `MethodInfo`,
        // `ConstructorInfo`, `PropertyInfo`, `Activator`, `Assembly`, `AppDomain`. `Regex` is on
        // none of it and constructs nothing that is. The one member that touches it,
        // `Regex.CompileToAssembly`, is unreachable **by §2's own mechanism rather than by luck**:
        // `ResolveMethod` resolves every parameter type through this same allowlist, and its
        // parameters are `RegexCompilationInfo[]`, `System.Reflection.AssemblyName` and
        // `System.Reflection.Emit.CustomAttributeBuilder[]` — none admitted — so the signature
        // lookup fails before the method is found. Exactly how `Binder` blocks
        // `Type.InvokeMember`. `DeserializationHardeningTest` pins it rather than arguing it.
        //
        // WHAT IS ACCEPTED, and it is real: a hostile payload may name `Regex.IsMatch` with a
        // catastrophic-backtracking pattern on an overload that takes no timeout. That is denial
        // of service, not code execution, and `security-review.md` §4 records it with the
        // deployer's mitigation. `RegexOptions` needs no entry — every enum is already admitted.
        typeof(System.Text.RegularExpressions.Regex),
    ];

    // Operation hosts that live in `EFCore.Relational`, which this assembly does not reference
    // (M9), so they cannot be named with `typeof`. Matched by full name AND assembly instead --
    // the same by-name route `ServerBoundaryAnalyzer` takes for `FromSqlQueryRootExpression`.
    //
    //   RelationalDbFunctionsExtensions -> EF.Functions.Collate / Least / Greatest
    //   EFExtensions                    -> EF.Constant / EF.Parameter / EF.MultipleParameters
    //
    // WHY THEY WERE MISSING, and it was not deliberate. `EF` and `DbFunctions` are both admitted
    // above, and so is the *core* `DbFunctionsExtensions` -- but the markers a caller actually
    // writes are declared on these two relational classes, so every `EF.Functions.Collate` and
    // every `EF.MultipleParameters` was refused at the client boundary and raised EF's own
    // `TranslationFailed`. The server could translate all of them: it is an ordinary relational
    // provider. This is the shape M9 J20 reversed for `Regex` -- a refusal that made this provider
    // disagree with every reference implementation -- and it cost six reds in
    // `NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest` alone.
    //
    // WHY THIS DOES NOT BREAK `security-review.md` §2's CONJUNCTION. That bound is over the
    // reflection *invocation* surface -- `Binder`, `MethodBase`, `MethodInfo`, `ConstructorInfo`,
    // `PropertyInfo`, `Activator`, `Assembly`, `AppDomain`. Neither class is on it, neither
    // derives from anything on it, and neither constructs anything on it. Their parameters are
    // `DbFunctions`, scalars, `string` and arrays. The generic ones (`EF.Constant<T>` and
    // friends) are bounded by §2's own mechanism rather than by luck: `ResolveMethod` resolves
    // every parameter type through this same allowlist, so a `T` bound to an unadmitted type
    // fails the signature lookup before the method is found. Naming a host permits the type to be
    // named; a method still has to resolve by signature.
    private static readonly HashSet<string> RelationalOperationHostNames =
    [
        "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions",
        "Microsoft.EntityFrameworkCore.EFExtensions",
    ];

    private static bool IsRelationalOperationHost(Type type)
        => type.FullName is { } name
            && RelationalOperationHostNames.Contains(name)
            && type.Assembly.GetName().Name == "Microsoft.EntityFrameworkCore.Relational";

    private static readonly HashSet<Type> BuiltInGenericDefinitions =
    [
        typeof(ParameterBox<>),
        typeof(Nullable<>), typeof(List<>), typeof(IList<>), typeof(ICollection<>),
        typeof(IEnumerable<>), typeof(IEnumerator<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
        typeof(IQueryable<>), typeof(IOrderedQueryable<>), typeof(IOrderedEnumerable<>),
        typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>), typeof(SortedSet<>),
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
        typeof(KeyValuePair<,>), typeof(IGrouping<,>), typeof(ILookup<,>),

        // This provider's own grouping (M9 J8). Admitting it is what keeps EF's internal
        // `GroupBySingleQueryingEnumerable+InternalGrouping` OUT of this list: the server projects
        // into `WireGrouping<,>` and only that name ever crosses. An EF internal type in an
        // ADR-008 constraint 2 allowlist would widen the conjunction `security-review.md` §2
        // describes; a public type of ours does not.
        typeof(WireGrouping<,>),
        typeof(ReadOnlyCollection<>), typeof(Collection<>),
        typeof(ImmutableArray<>), typeof(ImmutableList<>), typeof(ImmutableHashSet<>),
        typeof(ImmutableSortedSet<>), typeof(IImmutableSet<>), typeof(IImmutableList<>),
        typeof(Expression<>), typeof(EqualityComparer<>), typeof(Comparer<>),
        typeof(Microsoft.EntityFrameworkCore.Query.IIncludableQueryable<,>),
        typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>),
        // Up to the full nesting arity: the projection rewrite carries a row's server-evaluated
        // values in a tuple, and beyond seven values that tuple nests in its eighth argument.
        typeof(ValueTuple<>), typeof(ValueTuple<,>), typeof(ValueTuple<,,>), typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>), typeof(ValueTuple<,,,,,>), typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    private readonly HashSet<Type> _allowed;
    private readonly ConcurrentDictionary<Type, bool> _cache = new();

    private TypeAllowlist(HashSet<Type> allowed)
        => _allowed = allowed;

    /// <summary>
    ///     A copy of this list that also admits <paramref name="extra" />.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>ONE LIST, SO ONE DECOMPOSITION.</b> The application's registered projection types
    ///         (<c>AllowTypes</c> on the client, <c>AddInfoCarrierAllowedTypes</c> on the server)
    ///         reach the request path through <see cref="ForModel" />, and used to reach the
    ///         RESPONSE path as a second set that <c>TypeNodeResolver</c> consulted beside this
    ///         one. A second set cannot be decomposed: a raw-SQL join projects
    ///         <c>ValueTuple&lt;UnmappedCustomer, UnmappedOrder&gt;</c>, this list admits
    ///         <c>ValueTuple&lt;,&gt;</c> and asks whether each ARGUMENT is allowed, and the answer
    ///         came from the other set, which it could not see. Eight
    ///         <c>SqlQueryTestBase</c> tests failed on exactly that tuple.
    ///     </para>
    ///     <para>
    ///         <b>It widens nothing.</b> Every type here is one the application registered
    ///         explicitly, which <c>docs/security-review.md</c> §4c records as the safe shape, and
    ///         the same types already clear the request path. The conjunction §2 depends on is
    ///         untouched: none of <c>Binder</c>, <c>MethodBase</c>, <c>MethodInfo</c>,
    ///         <c>ConstructorInfo</c>, <c>PropertyInfo</c>, <c>Activator</c>, <c>Assembly</c> or
    ///         <c>AppDomain</c> can arrive this way, because a caller registering one of those has
    ///         already been trusted with the request path.
    ///     </para>
    /// </remarks>
    /// <param name="extra">The application's registered types. Empty returns this instance.</param>
    /// <returns>This instance when there is nothing to add, otherwise a widened copy.</returns>
    public TypeAllowlist With(IReadOnlyCollection<Type> extra)
    {
        ArgumentNullException.ThrowIfNull(extra);

        if (extra.Count == 0)
        {
            return this;
        }

        var widened = new HashSet<Type>(_allowed);
        widened.UnionWith(extra);
        return new TypeAllowlist(widened);
    }

    /// <summary>
    ///     Builds the allowlist for a model: every entity CLR type, every mapped property type,
    ///     every type <em>declaring</em> a mapped member, and any types the application registers
    ///     explicitly.
    /// </summary>
    /// <remarks>
    ///     The declaring type is not always the entity type. A mapped member may be inherited from
    ///     a class the model does not know — <c>PrincipalEntity : NonEntityBase</c>, where
    ///     <c>Reference</c> is declared on the base — and a member read names the type that
    ///     declares it, so refusing that type refuses a read of a perfectly ordinary mapped
    ///     navigation. It is not a widening of what the payload can reach either: the type is a
    ///     base of an entity type the list already admits, and the member it is named for is one
    ///     the model maps.
    /// </remarks>
    /// <param name="model">The EF model, or <see langword="null" /> when none is available.</param>
    /// <param name="registeredTypes">
    ///     Projection types the application declares — DTOs a query projects into, which are not
    ///     part of the model and cannot be inferred from it.
    /// </param>
    public static TypeAllowlist ForModel(IModel? model, IEnumerable<Type>? registeredTypes = null)
    {
        var allowed = new HashSet<Type>();

        if (model is not null)
        {
            foreach (IEntityType entityType in model.GetEntityTypes())
            {
                allowed.Add(entityType.ClrType);
                AddSupertypes(entityType.ClrType, allowed);

                foreach (IProperty property in entityType.GetProperties())
                {
                    allowed.Add(property.ClrType);
                    AddPropertyBaseTypes(property.ClrType, allowed);
                }

                // A complex type is part of the model but is not an entity type, so neither loop
                // above names it — and a complex value travels as itself, not as a key.
                AddComplexTypes(entityType, allowed);

                foreach (IPropertyBase member in entityType.GetMembers())
                {
                    if (member.PropertyInfo?.DeclaringType is { } fromProperty)
                    {
                        allowed.Add(fromProperty);
                    }

                    if (member.FieldInfo?.DeclaringType is { } fromField)
                    {
                        allowed.Add(fromField);
                    }

                    // A navigation need not be spelled as the entity type it targets.
                    if (member is INavigationBase)
                    {
                        AddDeclaredType(member.ClrType, allowed);
                    }
                }
            }
        }

        // The classes declaring the model's own `HasDbFunction` methods (R84).
        //
        // A user-defined function is an ordinary method on a class the model names, and that class
        // is almost never an entity type -- it is a `DbContext` subclass or a static helper. So
        // every mapped function was refused at the client boundary and `QuerySplitter` raised EF's
        // `TranslationFailed`, while the server -- an ordinary relational provider with the same
        // model -- translated it without difficulty. 81 red tests, and R74 recorded the symptom as
        // "this provider does not support `HasDbFunction`".
        //
        // WHY THIS IS NOT A WIDENING `security-review.md` §2 HAS TO RE-EXAMINE. It is model-derived,
        // like every entity type and property type above: the application named these methods in
        // its own `OnModelCreating`, and a payload that names one reaches a method the model maps
        // and nothing else. §2a's argument for C53 applies word for word, and the same guard is
        // applied for the same reason -- a declaring type on the reflection invocation surface is
        // refused rather than trusted, so the conjunction does not depend on nobody ever mapping
        // a function onto one.
        foreach (MethodInfo function in Metadata.ModelDbFunctions.ForModel(model))
        {
            if (function.DeclaringType is { } declaring && !IsReflectionInvocationSurface(declaring))
            {
                allowed.Add(declaring);
            }
        }

        foreach (Type type in registeredTypes ?? [])
        {
            allowed.Add(type);
        }

        return new TypeAllowlist(allowed);
    }

    /// <summary>
    ///     Admits the interfaces an entity CLR type implements and the classes it derives from.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A query may legitimately name an entity by a supertype rather than by the type the
    ///         model registered. <c>HasAction17794&lt;T&gt;() where T : IOffer</c> builds its
    ///         predicate against the interface, so the tree the client captures reads
    ///         <c>Convert(v, IOffer).OfferActions</c> — a cast whose target is not an entity CLR
    ///         type and was therefore refused, taking the whole <c>Where</c> to the client with it.
    ///     </para>
    ///     <para>
    ///         This is the same statement as the declaring-type rule above, from the other side:
    ///         a supertype of an entity type is reachable only through an instance the model
    ///         itself produced, so naming one widens nothing.
    ///         <c>QuerySplitter.InvalidIncludeFinder.IsEntity</c> asks the identical question with
    ///         <c>type.IsAssignableFrom(e.ClrType)</c>; this is that answer, precomputed.
    ///     </para>
    /// </remarks>
    /// <summary>
    ///     Admits the base classes of a mapped property's CLR type.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A member is often declared on a base class the model never names.</b>
    ///         <c>MultiLineStringEntity.MultiLineString</c> is a <c>MultiLineString</c>, and its
    ///         indexer is declared on <c>GeometryCollection</c> — an intermediate class between it
    ///         and <c>Geometry</c>. So <c>e.MultiLineString[0]</c> named a type the allowlist had
    ///         never heard of, the boundary analyzer refused the call, and the projection rewriter
    ///         shipped the whole geometry and evaluated the index on the client instead.
    ///     </para>
    ///     <para>
    ///         The argument is the one <see cref="AddSupertypes" /> already makes for entity
    ///         types, from the same side: <b>a base class of a mapped type is reachable only
    ///         through a value the model itself produced</b>, so naming one widens nothing a
    ///         payload could not already reach.
    ///     </para>
    ///     <para>
    ///         <b>Base classes only, and never a category.</b> Interfaces are left out because a
    ///         primitive property would drag in the whole generic-math surface for no gain, and
    ///         the category types are excluded because C23 measured that exact widening at
    ///         <b>145 → 186</b> on a neighbouring mechanism: widening to <c>ValueType</c> or
    ///         <c>Enum</c> names a kind of thing rather than a thing.
    ///     </para>
    /// </remarks>
    private static void AddPropertyBaseTypes(Type clrType, HashSet<Type> allowed)
    {
        for (Type? super = clrType.BaseType;
            super is not null && super != typeof(object) && !IsCategory(super);
            super = super.BaseType)
        {
            if (IsReflectionInvocationSurface(super))
            {
                // Belt and braces. `docs/security-review.md` §2 records that the allowlist's
                // safety is a *conjunction*: `System.Type` is admitted and every enum is, and the
                // only thing stopping a payload turning a `Type.GetType(...)` into a call is that
                // these types are not. A rule that admits base classes must not be able to
                // reintroduce one — which it could only do if an application mapped a property
                // whose CLR type derived from one, absurd but not impossible. Refusing here means
                // the conjunction does not depend on nobody ever doing that.
                break;
            }

            allowed.Add(super);
        }

        static bool IsCategory(Type type)
            => type == typeof(ValueType)
                || type == typeof(Enum)
                || type == typeof(Array)
                || type == typeof(Delegate)
                || type == typeof(MulticastDelegate);
    }

    /// <summary>
    ///     The types that would turn an admitted <see cref="Type" /> into an invocation
    ///     (<c>docs/security-review.md</c> §2). Never admitted by inference — only ever by an
    ///     application registering one explicitly, which is its own decision.
    /// </summary>
    private static bool IsReflectionInvocationSurface(Type type)
        => type == typeof(System.Reflection.Binder)
            || type == typeof(System.Reflection.MemberInfo)
            || type == typeof(System.Reflection.MethodBase)
            || type == typeof(System.Reflection.Assembly)
            || type == typeof(System.Reflection.Module)
            || type == typeof(AppDomain)
            || typeof(System.Reflection.MemberInfo).IsAssignableFrom(type)
            || typeof(System.Reflection.Assembly).IsAssignableFrom(type);

    private static void AddSupertypes(Type clrType, HashSet<Type> allowed)
    {
        foreach (Type @interface in clrType.GetInterfaces())
        {
            allowed.Add(@interface);
        }

        // `object` is a built-in already, and stopping there keeps the set to types the
        // application declared.
        for (Type? super = clrType.BaseType; super is not null && super != typeof(object); super = super.BaseType)
        {
            allowed.Add(super);
        }
    }

    /// <summary>
    ///     Admits the type a navigation is <em>declared</em> as, and the types that compose it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A navigation's CLR type is not always its target entity type.
    ///         <c>HasMany(p =&gt; (ICollection&lt;Child&gt;)p.ChildCollection)</c> maps a navigation
    ///         declared as <c>ICollection&lt;IChild&gt;</c> — EF supports it, and
    ///         <c>ThenInclude_with_interface_navigations</c> is the spec test that says so. Every
    ///         node of the resulting <c>Include</c>/<c>ThenInclude</c> chain is then spelled with
    ///         the <em>interface</em>: the member read yields an <c>ICollection&lt;IChild&gt;</c>
    ///         and <c>ThenInclude</c>'s lambda parameter is an <c>IChild</c>. An interface is not an
    ///         entity CLR type, so neither of the loops above names it, and the whole chain was
    ///         refused.
    ///     </para>
    ///     <para>
    ///         Which did not raise anything: an unshippable <c>Include</c> goes to the client, and
    ///         the splitter then re-derives only the one segment the residual reads. A chain
    ///         silently degrading to its first segment is a <em>wrong answer</em>, and it is what
    ///         B19 found. <see cref="Query.QuerySplitter" />'s <c>InvalidIncludeFinder.IsEntity</c>
    ///         had to learn the same lesson one level up.
    ///     </para>
    ///     <para>
    ///         Nothing widens: the model itself declares this member with this type, and an
    ///         interface constructs nothing.
    ///     </para>
    /// </remarks>
    private static void AddDeclaredType(Type type, HashSet<Type> allowed)
    {
        // Already present means already descended — and a self-referencing navigation would
        // otherwise recur forever.
        if (!allowed.Add(type) || !type.IsConstructedGenericType)
        {
            return;
        }

        foreach (Type argument in type.GetGenericArguments())
        {
            AddDeclaredType(argument, allowed);
        }
    }

    /// <summary>
    ///     Admits a structural type's complex properties, their own property types, and whatever
    ///     they hold in turn.
    /// </summary>
    /// <remarks>
    ///     Walked by hand rather than through <c>GetFlattenedComplexProperties()</c>, which stops
    ///     at a complex <em>collection</em> — "including those on non-collection complex types" is
    ///     its own summary. A third-level complex property reached through one is exactly what a
    ///     shorter walk missed.
    /// </remarks>
    private static void AddComplexTypes(ITypeBase type, HashSet<Type> allowed)
    {
        foreach (IComplexProperty complexProperty in type.GetComplexProperties())
        {
            // Both, because they differ for a collection: the property is a `List<T>` and the
            // complex type is `T`.
            allowed.Add(complexProperty.ClrType);
            allowed.Add(complexProperty.ComplexType.ClrType);

            foreach (IProperty property in complexProperty.ComplexType.GetProperties())
            {
                allowed.Add(property.ClrType);
            }

            AddComplexTypes(complexProperty.ComplexType, allowed);
        }
    }

    /// <summary>
    ///     Whether <paramref name="type" /> may be named by a wire payload.
    /// </summary>
    public bool IsAllowed(Type type)
        => _cache.GetOrAdd(type, Evaluate);

    private bool Evaluate(Type type)
    {
        // Structural forms delegate to their components, so `List<Customer>[]` is allowed
        // exactly when `Customer` is.
        if (type.IsArray)
        {
            return type.GetElementType() is { } element && IsAllowed(element);
        }

        if (type.IsByRef || type.IsPointer)
        {
            return type.GetElementType() is { } element && IsAllowed(element);
        }

        if (type.IsGenericParameter)
        {
            return true;
        }

        // An exact match wins before any decomposition. `ForModel` adds each entity type's CLR
        // type verbatim, and an entity type can perfectly well *be* a constructed generic — the
        // EF specification suites nest their models inside generic test bases, so `Root` is
        // really `GraphUpdatesTestBase<TFixture>+Root`. Decomposing that asks whether the open
        // definition is listed, which it never is, so every such model type was denied and the
        // whole query became unshippable. Nothing widens here: the set contains only what the
        // model declared.
        if (_allowed.Contains(type))
        {
            return true;
        }

        // A delegate is a signature, not a constructible payload — `Func<Customer, bool>` names
        // no behaviour of its own. Checked before the generic branch, which would otherwise
        // demand `Func<,>` itself be listed and deny every lambda in every query.
        if (typeof(Delegate).IsAssignableFrom(type))
        {
            return !type.IsConstructedGenericType
                || Array.TrueForAll(type.GetGenericArguments(), IsAllowed);
        }

        // An enum is data, not behaviour, and travels as its underlying value, so allowing it
        // cannot construct anything. Asked HERE rather than only at the end of this method,
        // because a type NESTED IN A GENERIC TYPE is itself a constructed generic type even when
        // it declares no type parameter of its own: `MappingQueryTestBase<TFixture>+ShipVia` has
        // `IsConstructedGenericType` true, so the branch below decomposed it, asked whether the
        // open definition was listed — which no enum ever is — and denied it. The rule this file
        // states ("every enum is already admitted") was therefore false for exactly the shape EF's
        // specification suites use, and `MappingQueryTestBase.Project_nullable_enum` is what
        // found it. The enclosing type's generic arguments say nothing about an enum's value, so
        // there is nothing here to decompose. This is the same statement as the exact-match branch
        // above, for a type admitted by rule rather than by the set.
        if (type.IsEnum)
        {
            return true;
        }

        if (type.IsConstructedGenericType)
        {
            return IsAllowed(type.GetGenericTypeDefinition())
                && Array.TrueForAll(type.GetGenericArguments(), IsAllowed);
        }

        if (type.IsGenericTypeDefinition)
        {
            return BuiltInGenericDefinitions.Contains(type) || _allowed.Contains(type);
        }

        // `typeof(X)` arrives as the runtime subclass RuntimeType, not Type itself. A Type
        // value is a name, not an instantiable payload.
        if (typeof(Type).IsAssignableFrom(type))
        {
            return true;
        }

        if (BuiltInTypes.Contains(type) || BuiltInOperationHosts.Contains(type)
            || IsRelationalOperationHost(type) || _allowed.Contains(type))
        {
            return true;
        }

        // Every enum has already been admitted above, before the generic decomposition that used
        // to hide the nested ones.
        return false;
    }
}
