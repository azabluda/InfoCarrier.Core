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
[JsonSerializable(typeof(DynamicPropertyValue))]
[JsonSerializable(typeof(EntityKeyNode))]
[JsonSerializable(typeof(NodeKind))]
[JsonSerializable(typeof(MemberKind))]
public partial class ExpressionJsonContext : JsonSerializerContext
{
}
