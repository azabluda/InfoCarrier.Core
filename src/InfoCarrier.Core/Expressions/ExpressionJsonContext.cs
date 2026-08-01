// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     System.Text.Json source-generation context for the expression DTO model
///     (requirements §4.5 AOT/trimming). All expression nodes, type nodes, and dynamic value
///     nodes serialize through this context so the default serializer is reflection-free
///     and trim-safe.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ExpressionNode))]
[JsonSerializable(typeof(ConstantNode))]
[JsonSerializable(typeof(ParameterNode))]
[JsonSerializable(typeof(MemberNode))]
[JsonSerializable(typeof(MethodNode))]
[JsonSerializable(typeof(MethodCallNode))]
[JsonSerializable(typeof(LambdaNode))]
[JsonSerializable(typeof(NewNode))]
[JsonSerializable(typeof(NewArrayNode))]
[JsonSerializable(typeof(BinaryNode))]
[JsonSerializable(typeof(UnaryNode))]
[JsonSerializable(typeof(TypeBinaryNode))]
[JsonSerializable(typeof(ConditionalNode))]
[JsonSerializable(typeof(MemberInitNode))]
[JsonSerializable(typeof(MemberBindingNode))]
[JsonSerializable(typeof(ListInitNode))]
[JsonSerializable(typeof(ElementInitNode))]
[JsonSerializable(typeof(InvocationNode))]
[JsonSerializable(typeof(QueryRootStubNode))]
[JsonSerializable(typeof(TypeNode))]
[JsonSerializable(typeof(DynamicValueNode))]
[JsonSerializable(typeof(List<DynamicValueNode>))]
[JsonSerializable(typeof(DynamicPropertyValue))]
[JsonSerializable(typeof(EntityKeyNode))]
[JsonSerializable(typeof(NodeKind))]
[JsonSerializable(typeof(MemberKind))]

// ConstantNode.PrimitiveValue is declared `object?`, so System.Text.Json resolves its
// JsonTypeInfo by the *runtime* type of each value. Under source generation every such type
// must be registered here or serialization throws NotSupportedException. The set below is
// exactly what ExpressionToNodeTranslator.IsPrimitive admits. Enums are not listed: they are
// normalized to their underlying integral value before serialization (a concrete enum type
// could never be pre-registered) and rebuilt by NodeToExpressionTranslator.CoercePrimitive
// from the TypeNode.
//
// Deliberately NOT solved with a reflection-based fallback resolver: that would silently
// defeat the AOT/trimming goal (requirements §4.5, ADR-008 constraint 8).
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(Guid))]
public partial class ExpressionJsonContext : JsonSerializerContext
{
}
