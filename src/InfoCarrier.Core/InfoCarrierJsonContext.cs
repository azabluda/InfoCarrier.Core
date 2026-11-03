// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core;

/// <summary>
///     Generated metadata for live protocol 4 envelopes, operations, and inline query/value graphs.
/// </summary>
/// <remarks>
///     The admitted primitive vocabulary is closed. Unknown runtime object types fail without
///     reflection fallback. Node references use explicit Id/Ref values rather than serializer
///     reference handling. Null omission and named floats match the node encoding. Four extra
///     depth levels allow operation wrappers around a graph whose standalone limit is 256.
///     Historical protocol 3 uses independent metadata and retains its original options.
/// </remarks>
[JsonSourceGenerationOptions(
    // Protocol-specific options belong to their context; do not apply these to protocol 3.
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    MaxDepth = 260,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InfoCarrierEnvelope))]
[JsonSerializable(typeof(Wire.QueryRequestIdentification))]
[JsonSerializable(typeof(InfoCarrierFault))]
[JsonSerializable(typeof(InfoCarrierOperation))]
[JsonSerializable(typeof(QueryDataRequest))]
[JsonSerializable(typeof(QueryDataResult))]
[JsonSerializable(typeof(SaveChangesRequest))]
[JsonSerializable(typeof(SaveChangesResult))]
[JsonSerializable(typeof(ChangeEntry))]
[JsonSerializable(typeof(GeneratedValues))]
[JsonSerializable(typeof(ServerLogEvent))]
[JsonSerializable(typeof(SavepointRequest))]
[JsonSerializable(typeof(TransactionResult))]
[JsonSerializable(typeof(QueryTrackingBehavior))]

// Not payload records, but payloads all the same: a transaction id is sent as a bare string, a
// savepoint-support answer as a bare bool, and `object` is what a void or faulted response carries
// (always as null -- nothing ever serializes a non-null `object` through here).
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
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
[JsonSerializable(typeof(FromSqlQueryRootStubNode))]
[JsonSerializable(typeof(SqlQueryRootStubNode))]
[JsonSerializable(typeof(ServerContextStubNode))]
[JsonSerializable(typeof(TypeNode))]
[JsonSerializable(typeof(DynamicValueNode))]
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

// A byte array is not in `IsPrimitive`'s set, but it *is* what a value converter over a binary
// key produces (`ComparableBytesStructKey` -> `byte[]`), and a key value travels as its provider
// value (`PrimitiveCoercion.ToWireKey`).
[JsonSerializable(typeof(byte[]))]

// `Uri` is the same statement for the same reason: `WrappedUriClassKey`'s converter produces one,
// so a `Uri` lands in `EntityKeyNode.KeyValues`, which is declared `object` and therefore resolved
// by *runtime* type. Without this the payload failed to serialize at all — "JsonTypeInfo metadata
// for type 'System.Uri' was not provided ... Path: $.EntityKey.KeyValues".
//
// Distinct from the value-mapper seam (ADR-012), which is what stops a `Uri` being *walked*
// reflectively as an object shape — `Uri.AbsolutePath` throws outright for a relative URI. The two
// cover different paths: the seam covers property values, this covers key values, and a wrapped
// `Uri` key needs both.
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(JsonElement))]
public partial class InfoCarrierJsonContext : JsonSerializerContext
{
}
