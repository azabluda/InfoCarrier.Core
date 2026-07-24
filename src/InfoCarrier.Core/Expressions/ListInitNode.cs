// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A single element initializer in a <see cref="ListInitNode" /> (one <c>Add</c> call's
///     arguments).
/// </summary>
public sealed record ElementInitNode
{
    /// <summary>
    ///     The <c>Add</c> method used to add the element.
    /// </summary>
    public required MethodNode AddMethod { get; init; }

    /// <summary>
    ///     The arguments to the <c>Add</c> call (usually a single element).
    /// </summary>
    public IReadOnlyList<ExpressionNode> Arguments { get; init; } = [];
}

/// <summary>
///     A list-init expression: <c>new List&lt;T&gt; { a, b, c }</c>.
/// </summary>
public sealed record ListInitNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.ListInit;

    /// <summary>
    ///     The collection construction being initialized.
    /// </summary>
    public required NewNode NewExpression { get; init; }

    /// <summary>
    ///     The element initializers.
    /// </summary>
    public IReadOnlyList<ElementInitNode> Initializers { get; init; } = [];

    /// <summary>
    ///     The result type.
    /// </summary>
    public required TypeNode Type { get; init; }
}
