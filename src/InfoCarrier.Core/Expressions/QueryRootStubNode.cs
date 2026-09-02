// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A query-root stub standing in for EF Core's <c>EntityQueryRootExpression</c>
///     (research-findings §2). Carries entity-type identity (<see cref="TypeNode.EntityTypeName" />
///     distinguishes shared-type entities); the server rebinds it to a real
///     <c>DbSet&lt;T&gt;</c> / <c>EntityQueryRootExpression</c> via its model.
/// </summary>
/// <remarks>
///     <b>Not sealed, and the one derived node is <see cref="FromSqlQueryRootStubNode" /></b>
///     (#60). A query root that carries state beyond its entity type is a subclass on EF's side
///     too, and the wire mirrors that rather than widening this node with fields most roots do not
///     have. <c>ServerBoundaryAnalyzer.IsSerializableKind</c> matches EF's root by its EXACT type
///     for the same reason: a subclass it does not know is refused rather than shipped with its
///     extra state silently dropped.
/// </remarks>
public record QueryRootStubNode : ExpressionNode
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
