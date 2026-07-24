// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A query-root stub standing in for EF Core's <c>EntityQueryRootExpression</c>
///     (research-findings §2). Carries entity-type identity (<see cref="TypeNode.EntityTypeName" />
///     distinguishes shared-type entities); the server rebinds it to a real
///     <c>DbSet&lt;T&gt;</c> / <c>EntityQueryRootExpression</c> via its model.
/// </summary>
public sealed record QueryRootStubNode : ExpressionNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.QueryRootStub;

    /// <summary>
    ///     The entity element type. Its <see cref="TypeNode.EntityTypeName" /> carries the EF
    ///     entity-type name (the shared-type discriminator, research-findings §3/§7).
    /// </summary>
    public required TypeNode ElementType { get; init; }

    /// <summary>
    ///     The queryable type (<c>IQueryable&lt;T&gt;</c>).
    /// </summary>
    public required TypeNode Type { get; init; }
}
