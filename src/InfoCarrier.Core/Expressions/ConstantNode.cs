// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A constant value. Primitive values serialize inline in <see cref="PrimitiveValue" />;
///     non-primitive values (entities, collections, closures, anonymous/DTO projections)
///     serialize as a shape-based <see cref="DynamicValueNode" /> (research-findings §7).
///     Compiled-query parameters are already substituted as plain constants (§6) — no wrapper
///     types (the v1 <c>ValueWrapper&lt;T&gt;</c> trap).
/// </summary>
public sealed record ConstantNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.Constant;

    /// <summary>
    ///     The runtime type of the constant value.
    /// </summary>
    public required TypeNode Type { get; init; }

    /// <summary>
    ///     The runtime type of <see cref="PrimitiveValue" />, when it is more specific than
    ///     <see cref="Type" />. Null when the two agree, which is the usual case.
    /// </summary>
    /// <remarks>
    ///     A constant declared as <c>object</c> says nothing about what it holds, and the
    ///     compact primitive form carries no type of its own — so a boxed <c>Guid</c> came back
    ///     as a string and every comparison against it was quietly false. EF builds exactly that
    ///     for <c>GetDatabaseValues</c> on a non-numeric key: <c>Equals(EF.Property&lt;object&gt;(e,
    ///     "Id"), keyValues[0])</c>, where the index expression is typed <c>object</c>. The
    ///     declared type still governs how the constant is rebuilt, because the surrounding tree
    ///     expects it.
    /// </remarks>
    public TypeNode? ValueType { get; init; }

    /// <summary>
    ///     The primitive value (string, number, bool, enum-underlying, null). Null when the
    ///     value is non-primitive — see <see cref="DynamicValue" />.
    /// </summary>
    public object? PrimitiveValue { get; init; }

    /// <summary>
    ///     The non-primitive value graph (entity-by-key, collection, anonymous/DTO shape).
    ///     Null when <see cref="PrimitiveValue" /> carries the value.
    /// </summary>
    public DynamicValueNode? DynamicValue { get; init; }
}
