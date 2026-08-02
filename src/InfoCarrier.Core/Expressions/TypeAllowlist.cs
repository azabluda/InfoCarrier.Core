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
    ///     and any types the application registers explicitly.
    /// </summary>
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

                foreach (IProperty property in entityType.GetProperties())
                {
                    allowed.Add(property.ClrType);
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
