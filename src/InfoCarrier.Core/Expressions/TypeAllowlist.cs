// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
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
    ];

    // Static holders that appear as a MethodNode's declaring type. A method still has to be
    // resolved by signature; this only permits the type to be named at all.
    private static readonly HashSet<Type> BuiltInOperationHosts =
    [
        typeof(Queryable), typeof(Enumerable), typeof(EF), typeof(DbFunctions),
        typeof(EntityFrameworkQueryableExtensions), typeof(Math), typeof(MathF),
        typeof(Convert), typeof(StringComparer), typeof(Expression),
        typeof(DbFunctionsExtensions),
    ];

    private static readonly HashSet<Type> BuiltInGenericDefinitions =
    [
        typeof(Nullable<>), typeof(List<>), typeof(IList<>), typeof(ICollection<>),
        typeof(IEnumerable<>), typeof(IEnumerator<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
        typeof(IQueryable<>), typeof(IOrderedQueryable<>), typeof(IOrderedEnumerable<>),
        typeof(HashSet<>), typeof(ISet<>), typeof(IReadOnlySet<>), typeof(SortedSet<>),
        typeof(Dictionary<,>), typeof(IDictionary<,>), typeof(IReadOnlyDictionary<,>),
        typeof(KeyValuePair<,>), typeof(IGrouping<,>), typeof(ILookup<,>),
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

        if (BuiltInTypes.Contains(type) || BuiltInOperationHosts.Contains(type) || _allowed.Contains(type))
        {
            return true;
        }

        // An enum is data, not behaviour, and travels as its underlying value anyway; allowing
        // it cannot construct anything.
        return type.IsEnum;
    }
}
