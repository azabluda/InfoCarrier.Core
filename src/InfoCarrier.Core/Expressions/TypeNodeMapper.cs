// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Maps CLR <see cref="Type" />s to assembly-free <see cref="TypeNode" /> identities.
///     DI-resolved; consults the EF model (when available) to populate
///     <see cref="TypeNode.EntityTypeName" /> so entity types carry model identity
///     (research-findings §7).
/// </summary>
public class TypeNodeMapper
{
    private readonly IModel? _model;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeNodeMapper" /> class.
    /// </summary>
    /// <param name="model">The EF model used to tag entity types; null when no model is in scope
    ///     (e.g. pure client-side value mapping).</param>
    public TypeNodeMapper(IModel? model = null)
        => _model = model;

    /// <summary>
    ///     The nearest type the wire can <em>name</em> for a value of <paramref name="type" />:
    ///     itself, unless it is a non-public implementation detail, in which case the nearest
    ///     public base.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>IPAddress.Loopback</c> is a <c>System.Net.IPAddress+ReadOnlyIPAddress</c> — a
    ///         private nested subclass — and EF's funcletizer types the constant by the value it
    ///         holds. No allowlist can admit that name and no transport could resolve it, so the
    ///         comparison went client-side and answered <b>0 rows instead of 1, silently</b> (B23).
    ///         What the caller wrote, and all the other side needs, is <c>IPAddress</c>.
    ///     </para>
    ///     <para>
    ///         <b>Two exclusions, and both were paid for.</b> A type whose first public base is
    ///         <see cref="object" /> is left alone: an anonymous type is also non-visible and is
    ///         not a private implementation of anything — it <em>is</em> the projection — and
    ///         widening one to <c>object</c> cost <b>92 in `GearsOfWarQuery`</b> (C9). And this is
    ///         **not** applied from <see cref="ToTypeNode" />, which every path shares: a lazy-loading
    ///         <b>proxy</b> is also non-visible with a public base, and rewriting one there cost
    ///         <b>375</b> — 107 `Load`, 77 `GraphUpdates`, 52 `OwnedQuery` — because
    ///         <c>DynamicValueMapper</c> already strips proxies deliberately, through the model,
    ///         and a second unrelated rewrite upstream fought it (C23, `c23-widening-reverted`).
    ///     </para>
    ///     <para>
    ///         So the two call sites are exact: the <em>constant</em> branch of
    ///         <c>WireTypeCollector</c>, and the value-mapper branch of
    ///         <c>DynamicValueMapper.MapToNode</c> — which every entity, proxy, primitive and
    ///         <see cref="Type" /> value has already returned before.
    ///     </para>
    /// </remarks>
    public static Type Nameable(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.IsVisible)
        {
            return type;
        }

        for (Type? super = type.BaseType; super is not null && !IsRoot(super); super = super.BaseType)
        {
            if (super.IsVisible)
            {
                return super;
            }
        }

        return type;
    }

    /// <summary>
    ///     The abstract roots of the type system, which are public but carry no information — so
    ///     standing in for a value with one loses the value.
    /// </summary>
    /// <remarks>
    ///     <c>object</c> is the one C9 measured at <b>92</b>. The rest were measured together at
    ///     <b>31</b> in <c>c23b</c>, and they are the same mistake one level out: an
    ///     <c>internal enum</c> widened to <see cref="Enum" /> — public, and useless — giving
    ///     <i>"The JSON value could not be converted to System.Enum"</i> 27 times, and a
    ///     compiler-generated array type widened to <see cref="Array" /> giving
    ///     <i>"Expression of type 'System.Array' cannot be used for parameter of type
    ///     'System.Collections.Generic…'"</i>. A base is a usable stand-in only when it is a real
    ///     type; these four are categories.
    /// </remarks>
    private static bool IsRoot(Type type)
        => type == typeof(object)
            || type == typeof(ValueType)
            || type == typeof(Enum)
            || type == typeof(Array)
            || type == typeof(Delegate)
            || type == typeof(MulticastDelegate);

    /// <summary>
    ///     Maps a CLR type to its wire identity.
    /// </summary>
    public virtual TypeNode ToTypeNode(Type type)
    {
        TypeNode result = type.IsGenericType && !type.IsGenericTypeDefinition
            ? new TypeNode
            {
                Name = type.GetGenericTypeDefinition().FullName ?? type.Name,
                GenericArguments = type.GetGenericArguments().Select(ToTypeNode).ToList(),
                EntityTypeName = LookupEntityTypeName(type),
            }
            : new TypeNode
            {
                Name = type.FullName ?? type.Name,
                EntityTypeName = LookupEntityTypeName(type),
            };

        return result;
    }

    private string? LookupEntityTypeName(Type type)
        => _model?.FindEntityType(type)?.Name;
}
