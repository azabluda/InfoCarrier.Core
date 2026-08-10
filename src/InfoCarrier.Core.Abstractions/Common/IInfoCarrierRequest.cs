// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common;

/// <summary>
///     Marks a wire type that travels <em>towards</em> the server — a message whose bytes a
///     caller controls.
/// </summary>
/// <remarks>
///     <para>
///         The distinction exists for the payload size bound (milestone M5,
///         <see cref="InfoCarrierPayloadLimits" />), and it is a real one rather than
///         bookkeeping. The threat this provider's wire hardening is against is stated in
///         roadmap M5 as "accepting serialized expression trees from remote clients": an
///         unauthenticated peer making a server allocate. A <em>result</em> travelling the other
///         way is something the client asked its own server for, and bounding it by the same
///         number is not that control — it is a page-size policy wearing its clothes.
///     </para>
///     <para>
///         Measured, not assumed: plan item C37 first applied one bound to both directions and
///         four Northwind spec tests went red at <b>560 MB</b> and <b>111 MB</b> — triple
///         cross-join results the client had asked for. The request direction never came close.
///     </para>
/// </remarks>
public interface IInfoCarrierRequest;
