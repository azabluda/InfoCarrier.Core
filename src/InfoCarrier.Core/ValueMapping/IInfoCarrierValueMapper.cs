// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.ValueMapping;

/// <summary>
///     A pluggable mapping between a CLR type the wire cannot walk and a value it can carry.
/// </summary>
/// <remarks>
///     <para>
///         The wire's default handling of a non-primitive, non-entity value is a reflective walk
///         of its public readable members (<c>DynamicValueMapper</c>'s object-shape branch).
///         That is right for an anonymous type, a record or a DTO, and wrong for a type whose
///         members are <em>computed</em>: <c>NetTopologySuite.Geometries.Geometry</c> exposes
///         <c>Boundary</c> and <c>Envelope</c>, both of which return geometries, and walking one
///         recurses until the stack overflows and the process dies. <c>System.Net.IPAddress</c>
///         is the same shape one step milder — its <c>ScopeId</c> throws
///         <c>SocketException</c> for an IPv4 address.
///     </para>
///     <para>
///         A mapper registered here gets first refusal on such a value and writes it as a single
///         wire primitive instead. Both directions return <see langword="bool" /> and both may
///         decline, so a value no registered mapper claims falls through to exactly the
///         behaviour it has today — registering one cannot change how anything else travels.
///     </para>
///     <para>
///         <strong>Registration is the application's, on both halves.</strong> The client's
///         mappers come from EF's internal service provider (add them alongside
///         <c>AddEntityFrameworkInfoCarrier</c>); the server's come from the service provider the
///         <c>IInfoCarrierServer</c> was built with. A value that crosses in both directions
///         needs the mapper on both sides — this is the same "computed twice by two providers"
///         property every wire fact here has, and the reason the contract is stated in terms of
///         the <em>CLR type alone</em>: neither side may consult a type mapping to decide.
///     </para>
///     <para>
///         This is deliberately how spatial support stays out of the product assembly: a WKT
///         geometry mapper is ~30 lines against NetTopologySuite, and it lives in the
///         application (or the test utilities) that already depends on it.
///     </para>
/// </remarks>
public interface IInfoCarrierValueMapper
{
    /// <summary>
    ///     Probes whether this mapper claims <paramref name="value" />, and if so produces the
    ///     value that travels in its place.
    /// </summary>
    /// <param name="value">The live value. Never <see langword="null" />.</param>
    /// <param name="declaredType">
    ///     The type the wire will name the value as — which is what the reverse direction is
    ///     handed, and therefore what a mapper should match on.
    /// </param>
    /// <param name="wireValue">
    ///     What travels: one of the primitives the wire's serializer context registers, in
    ///     practice a <see cref="string" /> (WKT, a text form, a URI) or a <c>byte[]</c> (WKB).
    ///     Must not be <see langword="null" /> when this method returns <see langword="true" />;
    ///     a mapper that has nothing to say declines instead.
    /// </param>
    /// <returns><see langword="true" /> if this mapper claims the value.</returns>
    bool TryMapToWire(object value, Type declaredType, out object? wireValue);

    /// <summary>
    ///     Probes whether this mapper claims a wire value named as <paramref name="declaredType" />,
    ///     and if so rebuilds the live value.
    /// </summary>
    /// <param name="wireValue">
    ///     What <see cref="TryMapToWire" /> produced — but note that after a serialization
    ///     round-trip it arrives as a <see cref="System.Text.Json.JsonElement" /> rather than as
    ///     the CLR type that was written, exactly as every other wire primitive does. Convert it
    ///     rather than casting it.
    /// </param>
    /// <param name="declaredType">The type the wire named the value as.</param>
    /// <param name="value">The rebuilt live value.</param>
    /// <returns><see langword="true" /> if this mapper claims the value.</returns>
    bool TryMapFromWire(object? wireValue, Type declaredType, out object? value);
}
