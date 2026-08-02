// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Builds the <see cref="ValueTuple" /> that carries a rewritten projection's server-side
///     values across the wire (<c>docs/projection-split.md</c> §3.2).
/// </summary>
/// <remarks>
///     <para>
///         A tuple is used rather than a purpose-built carrier because it is already on the
///         deserialization allowlist, and because construction is an ordinary
///         <see cref="NewExpression" /> — structurally the same thing an anonymous-type projection
///         is, which is the shape every provider already translates.
///     </para>
///     <para>
///         Above seven values the tuple nests in the usual way: the eighth type parameter is
///         itself a tuple holding the remainder, and its members are reached through
///         <c>Rest</c>.
///     </para>
/// </remarks>
internal static class TupleCarrier
{
    private const int MaxFlatArity = 7;

    private static readonly Type[] ValueDefinitions =
    [
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    /// <summary>
    ///     The reference-typed family, used when the carrier is compared to <see langword="null" />.
    /// </summary>
    private static readonly Type[] ReferenceDefinitions =
    [
        typeof(Tuple<>),
        typeof(Tuple<,>),
        typeof(Tuple<,,>),
        typeof(Tuple<,,,>),
        typeof(Tuple<,,,,>),
        typeof(Tuple<,,,,,>),
        typeof(Tuple<,,,,,,>),
        typeof(Tuple<,,,,,,,>),
    ];

    /// <summary>
    ///     The tuple type carrying <paramref name="types" />, nesting beyond seven.
    /// </summary>
    public static Type MakeType(IReadOnlyList<Type> types, bool nullable = false)
    {
        ArgumentOutOfRangeException.ThrowIfZero(types.Count);

        Type[] definitions = nullable ? ReferenceDefinitions : ValueDefinitions;

        if (types.Count <= MaxFlatArity)
        {
            return definitions[types.Count - 1].MakeGenericType([.. types]);
        }

        Type rest = MakeType([.. types.Skip(MaxFlatArity)], nullable);
        return definitions[MaxFlatArity].MakeGenericType([.. types.Take(MaxFlatArity), rest]);
    }

    /// <summary>
    ///     Constructs the tuple from <paramref name="values" />.
    /// </summary>
    public static Expression New(IReadOnlyList<Expression> values, bool nullable = false)
    {
        Type type = MakeType([.. values.Select(v => v.Type)], nullable);

        if (values.Count <= MaxFlatArity)
        {
            return Construct(type, values);
        }

        Expression rest = New([.. values.Skip(MaxFlatArity)], nullable);
        return Construct(type, [.. values.Take(MaxFlatArity), rest]);
    }

    /// <summary>
    ///     Builds the construction with its <see cref="NewExpression.Members" /> filled in.
    /// </summary>
    /// <remarks>
    ///     The members are the whole difference between a tuple EF can see through and one it
    ///     cannot. EF collapses <c>new { c, o }.c</c> back to <c>c</c> by looking the member up in
    ///     <see cref="NewExpression.Members" /> (<c>ReplacingExpressionVisitor.VisitMember</c>),
    ///     which is how an anonymous-type carrier survives navigation expansion at all. A tuple
    ///     built with the plain <c>Expression.New(ctor, args)</c> overload has none, so
    ///     <c>t.Item1</c> is an opaque field read, the entity behind it is lost, and the query
    ///     stops translating the moment anything navigates out of a slot — measured as
    ///     <b>214 extra failures</b>, all "could not be translated", before the members were
    ///     supplied.
    /// </remarks>
    private static Expression Construct(Type type, IReadOnlyList<Expression> values)
        => Expression.New(
            type.GetConstructors()[0],
            values,
            [.. Enumerable.Range(0, values.Count).Select(i => Member(type, i))]);

    /// <summary>
    ///     The member holding slot <paramref name="index" />: a field on a
    ///     <see cref="ValueTuple" />, a property on a <see cref="Tuple" />.
    /// </summary>
    private static MemberInfo Member(Type type, int index)
    {
        string name = index < MaxFlatArity ? $"Item{index + 1}" : "Rest";

        return type.IsValueType
            ? type.GetField(name)!
            : type.GetProperty(name)!;
    }

    /// <summary>
    ///     Reads the value at <paramref name="index" /> back out.
    /// </summary>
    public static Expression Read(Expression tuple, int index)
        => index < MaxFlatArity
            ? Expression.MakeMemberAccess(tuple, Member(tuple.Type, index))
            : Read(Expression.MakeMemberAccess(tuple, Member(tuple.Type, MaxFlatArity)), index - MaxFlatArity);
}
