// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json;
using InfoCarrier.Core;
using InfoCarrier.Core.Common;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.WireContract;

/// <summary>Authored entry points connect separately serialized bytes to their decoded contracts.</summary>
internal static class Coverage
{
    internal static readonly Type[] PrimitiveValues =
    [
        typeof(bool), typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan), typeof(Guid),
        typeof(byte[]), typeof(Uri), typeof(JsonElement),
    ];

    internal static IEnumerable<(Type Context, Type Root, WireDirection Direction)> Roots()
    {
        Type transport = typeof(InfoCarrierJsonContext);
        Type expressions = typeof(ExpressionJsonContext);
        yield return (transport, typeof(InfoCarrierEnvelope), WireDirection.Request | WireDirection.Response);
        yield return (transport, typeof(QueryDataRequest), WireDirection.Request);
        yield return (transport, typeof(InfoCarrier.Core.Wire.QueryRequestIdentification), WireDirection.Request);
        yield return (transport, typeof(QueryDataResult), WireDirection.Response);
        yield return (transport, typeof(SaveChangesRequest), WireDirection.Request);
        yield return (transport, typeof(SaveChangesResult), WireDirection.Response);
        yield return (transport, typeof(SavepointRequest), WireDirection.Request);
        yield return (transport, typeof(TransactionResult), WireDirection.Response);
        yield return (transport, typeof(string), WireDirection.Request);
        yield return (transport, typeof(bool), WireDirection.Response);
        // BeginTransaction has a null request. Acknowledgements and faults have null payloads.
        yield return (transport, typeof(object), WireDirection.Request | WireDirection.Response);
        yield return (transport, typeof(InfoCarrierFault), WireDirection.Response);
        yield return (transport, typeof(ServerLogEvent), WireDirection.Response);
        // QueryDataRequest.SerializedQuery is decoded independently of the outer byte[].
        yield return (expressions, typeof(ExpressionNode), WireDirection.Request);
        // QueryDataResult.SerializedResults, including the buffered form reconstructed from frames.
        yield return (expressions, typeof(List<DynamicValueNode>), WireDirection.Response);
        // ChangeEntry values/original values, GeneratedValues, and row-frame values.
        yield return (expressions, typeof(DynamicValueNode), WireDirection.Request | WireDirection.Response);
        // Kind is ignored as a property; its numeric assignments still define the node vocabulary.
        yield return (expressions, typeof(NodeKind), WireDirection.Request);
        Type oldTransport = typeof(InfoCarrier.Core.Wire.V3.Protocol3JsonContext);
        Type oldExpressions = typeof(InfoCarrier.Core.Wire.V3.Expressions.Protocol3ExpressionJsonContext);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.InfoCarrierEnvelope), WireDirection.Request | WireDirection.Response);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.QueryDataRequest), WireDirection.Request);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.QueryDataResult), WireDirection.Response);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.SaveChangesRequest), WireDirection.Request);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.SaveChangesResult), WireDirection.Response);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.SavepointRequest), WireDirection.Request);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.TransactionResult), WireDirection.Response);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.InfoCarrierFault), WireDirection.Response);
        yield return (oldTransport, typeof(InfoCarrier.Core.Wire.V3.Common.ServerLogEvent), WireDirection.Response);
        yield return (oldTransport, typeof(string), WireDirection.Request);
        yield return (oldTransport, typeof(bool), WireDirection.Response);
        yield return (oldTransport, typeof(object), WireDirection.Request | WireDirection.Response);
        yield return (oldExpressions, typeof(InfoCarrier.Core.Wire.V3.Expressions.ExpressionNode), WireDirection.Request);
        yield return (oldExpressions, typeof(List<InfoCarrier.Core.Wire.V3.Expressions.DynamicValueNode>), WireDirection.Response);
        yield return (oldExpressions, typeof(InfoCarrier.Core.Wire.V3.Expressions.DynamicValueNode), WireDirection.Request | WireDirection.Response);
        yield return (oldExpressions, typeof(InfoCarrier.Core.Wire.V3.Expressions.NodeKind), WireDirection.Request);
    }

    internal static Dictionary<string, string> Inventory() => new(StringComparer.Ordinal)
    {
        ["inline:QueryDataRequest.Query"] = "InfoCarrierJsonContext/ExpressionNode;Request",
        ["inline:QueryDataResult.Rows"] = "InfoCarrierJsonContext/IReadOnlyList<DynamicValueNode>;Response",
        ["nested:V3.QueryDataRequest.SerializedQuery"] = "Protocol3ExpressionJsonContext/ExpressionNode;Request",
        ["nested:V3.QueryDataResult.SerializedResults"] = "Protocol3ExpressionJsonContext/List<DynamicValueNode>;Response",
        ["nested:V3.ChangeEntry.SerializedValues"] = "Protocol3ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:V3.ChangeEntry.SerializedOriginalValues"] = "Protocol3ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:V3.GeneratedValues.SerializedValues"] = "Protocol3ExpressionJsonContext/DynamicValueNode;Response",
        ["nested:ChangeEntry.SerializedValues"] = "ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:ChangeEntry.SerializedOriginalValues"] = "ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:GeneratedValues.SerializedValues"] = "ExpressionJsonContext/DynamicValueNode;Response",
        ["runtime:InfoCarrierJsonContext.Object"] = "null," + string.Join(",", PrimitiveValues.Select(t => t.ToString()).Order()),
        ["runtime:Protocol3JsonContext.Object"] = "null",
        ["runtime:Protocol3ExpressionJsonContext.Object"] = string.Join(",", PrimitiveValues.Select(t => t.ToString()).Order()),
        ["runtime:ExpressionJsonContext.Object"] = string.Join(",", PrimitiveValues.Select(t => t.ToString()).Order()),
        ["exclusion:JsonElement"] = "Application-supplied JSON content; only its outer JSON representation is covered.",
    };
}
