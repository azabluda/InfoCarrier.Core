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
    }

    internal static Dictionary<string, string> Inventory() => new(StringComparer.Ordinal)
    {
        ["nested:QueryDataRequest.SerializedQuery"] = "ExpressionJsonContext/ExpressionNode;Request",
        ["nested:QueryDataResult.SerializedResults"] = "ExpressionJsonContext/List<DynamicValueNode>;Response",
        ["nested:ChangeEntry.SerializedValues"] = "ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:ChangeEntry.SerializedOriginalValues"] = "ExpressionJsonContext/DynamicValueNode;Request",
        ["nested:GeneratedValues.SerializedValues"] = "ExpressionJsonContext/DynamicValueNode;Response",
        ["runtime:InfoCarrierJsonContext.Object"] = "null",
        ["runtime:ExpressionJsonContext.Object"] = string.Join(",", PrimitiveValues.Select(t => t.ToString()).Order()),
        ["exclusion:JsonElement"] = "Application-supplied JSON content; only its outer JSON representation is covered.",
    };
}
