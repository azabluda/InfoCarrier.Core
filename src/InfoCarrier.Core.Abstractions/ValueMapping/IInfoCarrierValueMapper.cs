// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.ValueMapping;

/// <summary>
///     Maps a CLR value that the wire cannot walk to a value that it can carry.
/// </summary>
/// <remarks>
///     Register the same mapper through <c>InfoCarrierDeploymentBuilder.AddValueMappers</c> on
///     both deployment roles. A mapper may decline a value and leave it to later mappers.
/// </remarks>
public interface IInfoCarrierValueMapper
{
    /// <summary>
    ///     Tries to replace a live value with a wire value.
    /// </summary>
    bool TryMapToWire(object value, Type declaredType, out object? wireValue);

    /// <summary>
    ///     Tries to rebuild a live value from a wire value.
    /// </summary>
    bool TryMapFromWire(object? wireValue, Type declaredType, out object? value);
}
