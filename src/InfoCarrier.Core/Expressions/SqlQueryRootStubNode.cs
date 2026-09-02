// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A query-root stub carrying the caller's raw SQL for a <em>scalar</em> result (#56),
///     standing in for EF Core's relational <c>SqlQueryRootExpression</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>The sibling of <see cref="FromSqlQueryRootStubNode" />, and it exists because the
///         two roots are not interchangeable.</b> <c>Database.SqlQuery&lt;T&gt;</c> builds one of
///         two roots: EF builds <c>SqlQueryRootExpression</c> when the type-mapping source
///         recognises <c>T</c> — the scalar case, this node — and
///         <c>FromSqlQueryRootExpression</c> over an ad-hoc entity type otherwise. The difference
///         that matters here is that a scalar root has <b>no entity type</b>, so
///         <c>ServerQueryExecutor.RebindQueryRoot</c> must answer it before it looks one up.
///     </para>
///     <para>
///         <b>Named by shape rather than by type</b>, exactly as its sibling is:
///         <c>SqlQueryRootExpression</c> lives in <c>Microsoft.EntityFrameworkCore.Relational</c>,
///         which <c>InfoCarrier.Core</c> deliberately does not reference (<c>architecture.md</c>
///         section 6a D3, M9 J5). See <see cref="RelationalQueryRootShape" />.
///     </para>
///     <para>
///         <b>This node is a grant, not a translation</b>, and the grant is the same one its
///         sibling carries: a payload holding one asks the server to run a string it did not
///         write. A server rebuilds this node only if it registered
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />,
///         and a client sends one only if it called
///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />. Both
///         default to refusing. Read <c>docs/security-review.md</c> section 5a.
///     </para>
/// </remarks>
public sealed record SqlQueryRootStubNode : QueryRootStubNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.SqlQueryRootStub;

    /// <summary>
    ///     The caller's SQL text, verbatim.
    /// </summary>
    public required string Sql { get; init; }

    /// <summary>
    ///     The arguments EF collected for the SQL.
    /// </summary>
    /// <remarks>
    ///     Carried as a node rather than as a value list for the reason
    ///     <see cref="FromSqlQueryRootStubNode.Arguments" /> gives: a value spliced into text has
    ///     lost its type, and the server hands these back to EF to bind as <c>DbParameter</c>s.
    /// </remarks>
    public required ExpressionNode Arguments { get; init; }
}
