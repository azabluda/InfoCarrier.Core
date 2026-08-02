// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

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

    private static readonly Type[] Definitions =
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
    ///     The tuple type carrying <paramref name="types" />, nesting beyond seven.
    /// </summary>
    public static Type MakeType(IReadOnlyList<Type> types)
    {
        ArgumentOutOfRangeException.ThrowIfZero(types.Count);

        if (types.Count <= MaxFlatArity)
        {
            return Definitions[types.Count - 1].MakeGenericType([.. types]);
        }

        Type rest = MakeType([.. types.Skip(MaxFlatArity)]);
        return Definitions[MaxFlatArity].MakeGenericType([.. types.Take(MaxFlatArity), rest]);
    }

    /// <summary>
    ///     Constructs the tuple from <paramref name="values" />.
    /// </summary>
    public static Expression New(IReadOnlyList<Expression> values)
    {
        Type type = MakeType([.. values.Select(v => v.Type)]);

        if (values.Count <= MaxFlatArity)
        {
            return Expression.New(type.GetConstructors()[0], values);
        }

        Expression rest = New([.. values.Skip(MaxFlatArity)]);
        return Expression.New(type.GetConstructors()[0], [.. values.Take(MaxFlatArity), rest]);
    }

    /// <summary>
    ///     Reads the value at <paramref name="index" /> back out.
    /// </summary>
    public static Expression Read(Expression tuple, int index)
        => index < MaxFlatArity
            ? Expression.Field(tuple, $"Item{index + 1}")
            : Read(Expression.Field(tuple, "Rest"), index - MaxFlatArity);
}
