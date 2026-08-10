// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;

namespace InfoCarrier.Core;

/// <summary>
///     The size bound on a payload this process will deserialize (milestone M5, requirements
///     §4.1). A security control on a deserializer, so it is <b>default-on and opt-out</b>, like
///     the type and method allowlists.
/// </summary>
/// <remarks>
///     <para>
///         <b>Size is the half that was missing.</b> The <em>depth</em> bound has been in place
///         and load-bearing for some time — <c>ExpressionJsonContext</c> sets
///         <c>MaxDepth = 256</c>, raised from System.Text.Json's default of 64 because the node
///         model spends roughly four JSON levels on every hop between entities. Depth caps how
///         far a payload can recurse; it says nothing about how much it can be. A flat array of a
///         hundred million constants is depth 3.
///     </para>
///     <para>
///         <b>Why a byte count and not a node count.</b> The bound has to be checkable
///         <em>before</em> parsing begins, because the memory a parse costs is what it is
///         bounding; a node count is only knowable by parsing. The guards are O(1) length tests
///         on bytes already in hand.
///     </para>
///     <para>
///         <b>Why two numbers.</b> The direction is the whole of the threat model. Roadmap M5
///         states it as "accepting serialized expression trees from remote clients" — an
///         unauthenticated peer making a server allocate. A result travelling back is something
///         the client asked its own server for; bounding it by the same number is a page-size
///         policy, not a security control, and it is one this provider has no basis to set. So
///         <see cref="MaxRequestBytes" /> defaults to a number and
///         <see cref="MaxResponseBytes" /> defaults to <see langword="null" />.
///     </para>
///     <para>
///         That split is measured rather than reasoned-into. Plan item C37 first applied one
///         bound to both directions: four Northwind spec tests went red on results of
///         <b>560 MB</b> and <b>111 MB</b> — triple cross-joins the caller had asked for — while
///         no request came near the bound. A control that has to be widened past half a gigabyte
///         to let the suite pass is bounding the wrong direction.
///     </para>
///     <para>
///         <b>The default.</b> 64 MiB for a request. A query tree is kilobytes and a
///         <c>SaveChanges</c> request is bounded by the graph the caller tracked, so no legitimate
///         request approaches it. v1 had no bound at all and needed a 10 MB thread stack to
///         survive payloads over 1 MB (requirements §4.1) — the failure mode this replaces is a
///         stack overflow, which is not catchable.
///     </para>
/// </remarks>
public sealed class InfoCarrierPayloadLimits
{
    /// <summary>
    ///     The default maximum request size: 64 MiB. See the remarks on
    ///     <see cref="InfoCarrierPayloadLimits" /> for why this number and why only this direction.
    /// </summary>
    public const int DefaultMaxRequestBytes = 64 * 1024 * 1024;

    /// <summary>
    ///     The limits applied where none has been configured.
    /// </summary>
    public static readonly InfoCarrierPayloadLimits Default = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierPayloadLimits" /> class.
    /// </summary>
    /// <param name="maxRequestBytes">
    ///     The maximum size, in bytes, of a message towards the server
    ///     (<see cref="IInfoCarrierRequest" />) that this process will deserialize. Pass
    ///     <see langword="null" /> to opt out — a deliberate decision to accept a payload of any
    ///     size from whatever can reach this endpoint, which is why it is spelled as an explicit
    ///     <see langword="null" /> rather than as a very large number.
    /// </param>
    /// <param name="maxResponseBytes">
    ///     The maximum size, in bytes, of a message travelling back to the client. Null by
    ///     default: the client asked for the result, and this library has no basis for capping how
    ///     large an answer an application's own query may have. Set it to impose a page-size
    ///     policy.
    /// </param>
    public InfoCarrierPayloadLimits(
        int? maxRequestBytes = DefaultMaxRequestBytes,
        int? maxResponseBytes = null)
    {
        Positive(maxRequestBytes, nameof(maxRequestBytes));
        Positive(maxResponseBytes, nameof(maxResponseBytes));

        MaxRequestBytes = maxRequestBytes;
        MaxResponseBytes = maxResponseBytes;

        static void Positive(int? value, string name)
        {
            if (value is <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    name, value, "The maximum payload size must be positive. Pass null to opt out of the bound.");
            }
        }
    }

    /// <summary>
    ///     The maximum size, in bytes, of a message towards the server;
    ///     <see langword="null" /> when the bound has been opted out of.
    /// </summary>
    public int? MaxRequestBytes { get; }

    /// <summary>
    ///     The maximum size, in bytes, of a message back to the client; <see langword="null" />
    ///     unless the application set one.
    /// </summary>
    public int? MaxResponseBytes { get; }

    /// <summary>
    ///     Whether <typeparamref name="T" /> travels towards the server, and so is bounded by
    ///     <see cref="MaxRequestBytes" /> rather than <see cref="MaxResponseBytes" />.
    /// </summary>
    public static bool IsRequest<T>() => RequestMarker<T>.Value;

    /// <summary>
    ///     Refuses an oversized payload before any of it is parsed.
    /// </summary>
    /// <typeparam name="T">The type about to be deserialized; decides which bound applies.</typeparam>
    /// <param name="payloadLength">The payload length in bytes.</param>
    /// <param name="what">
    ///     What the payload is, named in the message so the refusal says which of several
    ///     deserialization points refused it.
    /// </param>
    /// <exception cref="InvalidOperationException">The payload exceeds the applicable maximum.</exception>
    public void Guard<T>(int payloadLength, string what)
        => Guard(
            payloadLength,
            what,
            IsRequest<T>() ? MaxRequestBytes : MaxResponseBytes,
            IsRequest<T>() ? nameof(MaxRequestBytes) : nameof(MaxResponseBytes));

    /// <summary>
    ///     Refuses an oversized message towards the server. The overload for a payload that is
    ///     not typed at the call site — the serialized query tree, which is a bare
    ///     <see cref="byte" /> array inside a request.
    /// </summary>
    public void GuardRequest(int payloadLength, string what)
        => Guard(payloadLength, what, MaxRequestBytes, nameof(MaxRequestBytes));

    private static void Guard(int payloadLength, string what, int? max, string limitName)
    {
        if (max is { } limit && payloadLength > limit)
        {
            // Both numbers, and which limit: a refusal naming neither the size nor the bound is
            // indistinguishable from a corrupt payload to whoever has to raise the limit.
            throw new InvalidOperationException(
                $"The {what} is {payloadLength} bytes, which exceeds the maximum of {limit} bytes "
                + $"(InfoCarrierPayloadLimits.{limitName}). Raise the limit on the configured "
                + "serializer, or pass null to opt out of it.");
        }
    }

    /// <summary>
    ///     Caches the marker test per closed generic type, so the guard on a hot deserialization
    ///     path is a static field read rather than a reflection call.
    /// </summary>
    private static class RequestMarker<T>
    {
        internal static readonly bool Value = typeof(IInfoCarrierRequest).IsAssignableFrom(typeof(T));
    }
}
