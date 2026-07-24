// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core;

/// <summary>
///     Translates <see cref="Expression" /> trees to and from the serializable
///     <see cref="ExpressionNode" /> DTO model (expression-serialization §3). This is the
///     bidirectional seam between the client's raw-captured LINQ tree (ADR-006) and the wire.
/// </summary>
/// <remarks>
///     DI-resolved — no statics (rlinq's <c>TypeResolver.Instance</c> is the anti-pattern).
///     Implementations must produce <em>canonical, deterministic</em> output so a
///     compiled-query cache can key off the serialized form (research-findings §10 / Q5).
/// </remarks>
public interface IExpressionSerializer
{
    /// <summary>
    ///     Translates a live expression tree to its serializable DTO.
    /// </summary>
    /// <param name="expression">The expression to translate.</param>
    /// <returns>The serializable node DTO.</returns>
    ExpressionNode ToNode(Expression expression);

    /// <summary>
    ///     Translates a serializable node DTO back to a live expression tree. Query-root stubs
    ///     are rebound to the server's model during this translation (research-findings §2).
    /// </summary>
    /// <param name="node">The node DTO.</param>
    /// <returns>The live expression tree.</returns>
    Expression ToExpression(ExpressionNode node);
}
