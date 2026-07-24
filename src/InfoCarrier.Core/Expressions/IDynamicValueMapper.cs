// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Maps non-primitive constant values to and from <see cref="DynamicValueNode" /> graphs
///     (expression-serialization §3.1 EF-metadata-driven). Implemented by
///     <c>DynamicValueMapper</c> (B5); declared as a seam so <see cref="ExpressionToNodeTranslator" />
///     and the reverse translator stay decoupled from value-mapping internals.
/// </summary>
public interface IDynamicValueMapper
{
    /// <summary>
    ///     Maps a live value to its shape-based / entity-keyed node graph.
    /// </summary>
    DynamicValueNode ToDynamicValue(object? value, Type type);

    /// <summary>
    ///     Materializes a live value from its node graph.
    /// </summary>
    object? FromDynamicValue(DynamicValueNode node);
}
