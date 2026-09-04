// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Abstract base of the serializable expression-node DTO model (rlinq-style node DTOs,
///     greenfield per ADR-001/ADR-008). Reflection objects never cross the wire — only these
///     DTOs do. Polymorphic serialization is via <see cref="NodeKind" /> discriminator.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(ConstantNode), (int)NodeKind.Constant)]
[JsonDerivedType(typeof(ParameterNode), (int)NodeKind.Parameter)]
[JsonDerivedType(typeof(MemberNode), (int)NodeKind.Member)]
[JsonDerivedType(typeof(MethodCallNode), (int)NodeKind.MethodCall)]
[JsonDerivedType(typeof(LambdaNode), (int)NodeKind.Lambda)]
[JsonDerivedType(typeof(NewNode), (int)NodeKind.New)]
[JsonDerivedType(typeof(NewArrayNode), (int)NodeKind.NewArray)]
[JsonDerivedType(typeof(BinaryNode), (int)NodeKind.Binary)]
[JsonDerivedType(typeof(UnaryNode), (int)NodeKind.Unary)]
[JsonDerivedType(typeof(TypeBinaryNode), (int)NodeKind.TypeBinary)]
[JsonDerivedType(typeof(ConditionalNode), (int)NodeKind.Conditional)]
[JsonDerivedType(typeof(MemberInitNode), (int)NodeKind.MemberInit)]
[JsonDerivedType(typeof(ListInitNode), (int)NodeKind.ListInit)]
[JsonDerivedType(typeof(InvocationNode), (int)NodeKind.Invocation)]
[JsonDerivedType(typeof(QueryRootStubNode), (int)NodeKind.QueryRootStub)]
[JsonDerivedType(typeof(FromSqlQueryRootStubNode), (int)NodeKind.FromSqlQueryRootStub)]
[JsonDerivedType(typeof(SqlQueryRootStubNode), (int)NodeKind.SqlQueryRootStub)]
[JsonDerivedType(typeof(ServerContextStubNode), (int)NodeKind.ServerContextStub)]
public abstract record ExpressionNode
{
    /// <summary>
    ///     The node kind discriminator.
    /// </summary>
    [JsonIgnore]
    public abstract NodeKind Kind { get; }
}
