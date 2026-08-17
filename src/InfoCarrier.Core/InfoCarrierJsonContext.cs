// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Text.Json.Serialization;
using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace InfoCarrier.Core;

/// <summary>
///     System.Text.Json source-generation context for the envelope and every operation payload
///     (requirements §4.5 AOT/trimming).
/// </summary>
/// <remarks>
///     <para>
///         The sibling of <see cref="Expressions.ExpressionJsonContext" />, which covers the
///         expression tree. Between them, nothing this provider puts on the wire is serialized
///         reflectively — which is what a trimmed WebAssembly build requires, because the SDK sets
///         <c>JsonSerializerIsReflectionEnabledByDefault=false</c> there and reflective
///         serialization then throws rather than degrading.
///     </para>
///     <para>
///         <b>The type set is closed, and it was read off the call sites rather than guessed.</b>
///         Every generic argument <see cref="IInfoCarrierSerializer" /> is ever instantiated with
///         is one of the types below: the six request/response records, the envelope itself,
///         <see cref="string" /> (a transaction id travels as a bare payload),
///         <see cref="bool" /> (<c>SupportsSavepoints</c>), and <see cref="object" /> — the last
///         from <c>Serialize&lt;object?&gt;(null)</c>, which is how both a void operation and a
///         faulted response fill <see cref="InfoCarrierEnvelope.Payload" />.
///         <see cref="InfoCarrierFault" />, <see cref="ChangeEntry" /> and
///         <see cref="GeneratedValues" /> are here because they are reachable as members.
///     </para>
///     <para>
///         <b>A missing type fails loudly, so the spec suite is the proof of closure.</b> Once a
///         resolver is set, System.Text.Json does not quietly fall back to reflection for a type
///         the context does not declare — it throws <c>"JsonTypeInfo metadata for type 'X' was not
///         provided by TypeInfoResolver"</c>, naming the type. That is how the <c>Uri</c> entry in
///         <see cref="Expressions.ExpressionJsonContext" /> was found, and it means the 22,453-test
///         suite — which drives this serializer on every hop — cannot pass with the set incomplete.
///     </para>
///     <para>
///         <b><see cref="ReferenceHandler.Preserve" /> is deliberately NOT carried over from the
///         options this replaced, and that is a wire change.</b> It cannot be: a source generator
///         cannot call an <c>init</c> accessor after construction, so it sets <c>required</c>/
///         <c>init</c> members through an object initializer, which System.Text.Json treats as
///         parameterized construction — and reference handling is unsupported there. The refusal
///         is structural rather than data-dependent: a round trip of an envelope carrying a fault
///         fails with <c>"Reference metadata is not supported when deserializing constructor
///         parameters … Path: $.fault.$ref"</c> even though the document it just wrote contains no
///         <c>$ref</c> at all, only <c>"fault":{"$id":"2",…}</c>.
///     </para>
///     <para>
///         <b>Dropping it costs nothing, because nothing at this layer was using it.</b> Every
///         nested graph — the expression tree, dynamic values, query results — is serialized
///         through <see cref="Expressions.ExpressionJsonContext" />, whose own options set no
///         reference handler, and reaches this layer already reduced to a <c>byte[]</c>
///         (<c>SerializedQuery</c>, <c>SerializedResults</c>, <c>SerializedValues</c>). What this
///         serializer actually sees is flat records with no repeated instance in them:
///         <see cref="InfoCarrierFault.Inner" /> is a chain of distinct objects, and
///         <c>SaveChangesRequest.Entries</c> a list of distinct ones. The node model handles its
///         own repeats with its own <c>Ref</c> mechanism, not with System.Text.Json's.
///     </para>
///     <para>
///         <see cref="JsonSourceGenerationMode.Metadata" /> rather than the default: the generated
///         <em>fast path</em> does not support several of the options a context may carry, and the
///         metadata path is what a resolver-driven serializer needs anyway.
///     </para>
/// </remarks>
[JsonSourceGenerationOptions(
    // These mirror what SystemTextJsonInfoCarrierSerializer configured before this context
    // existed, minus ReferenceHandler.Preserve -- see the remarks above for why that one cannot
    // come along and why it was not doing anything. A context carries its own options (the same
    // reason ExpressionJsonContext gives for MaxDepth and NumberHandling living on it rather than
    // on the serializer), so changing one of these changes the wire format.
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(InfoCarrierEnvelope))]
[JsonSerializable(typeof(InfoCarrierFault))]
[JsonSerializable(typeof(InfoCarrierOperation))]
[JsonSerializable(typeof(QueryDataRequest))]

// QueryDataResult is deliberately absent, and its absence is the wire change D7 half (A) made.
// It is no longer an envelope payload: a query response is a QueryStreamItem array written as the
// rows are produced, and it is `ExpressionJsonContext` that covers it -- see the note there for
// why it has to be that context and not this one.
[JsonSerializable(typeof(SaveChangesRequest))]
[JsonSerializable(typeof(SaveChangesResult))]
[JsonSerializable(typeof(ChangeEntry))]
[JsonSerializable(typeof(GeneratedValues))]
[JsonSerializable(typeof(SavepointRequest))]
[JsonSerializable(typeof(TransactionResult))]
[JsonSerializable(typeof(QueryTrackingBehavior))]

// Not payload records, but payloads all the same: a transaction id is sent as a bare string, a
// savepoint-support answer as a bare bool, and `object` is what a void or faulted response carries
// (always as null -- nothing ever serializes a non-null `object` through here).
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(object))]
public partial class InfoCarrierJsonContext : JsonSerializerContext
{
}
