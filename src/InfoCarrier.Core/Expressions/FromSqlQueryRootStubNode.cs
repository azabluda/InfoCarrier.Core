// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     A query-root stub carrying the caller's raw SQL (#60), standing in for EF Core's relational
///     <c>FromSqlQueryRootExpression</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Named by shape rather than by type.</b> <c>FromSqlQueryRootExpression</c> lives in
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, which <c>InfoCarrier.Core</c>
///         deliberately does not reference (<c>architecture.md</c> section 6a D3, M9 J5). What it
///         adds to a plain entity root is exactly two things - a SQL string and one argument
///         expression - and those are what this node carries. See
///         <see cref="RelationalQueryRootShape" />.
///     </para>
///     <para>
///         <b>This node is a grant, not a translation.</b> A payload carrying one asks the server
///         to run a string it did not write. <c>Sqlite/RawSqlExecutionProbeTest</c> (R94) measures
///         what that means: one <c>CommandText</c> runs every statement it contains, and an
///         uncomposed <c>FromSqlRaw</c> reaches the store unwrapped - so this is arbitrary SQL
///         execution and there is no read-only subset of it. A server rebuilds this node only if
///         it registered
///         <see cref="InfoCarrierServiceCollectionExtensions.AddInfoCarrierArbitrarySqlExecution" />,
///         and a client sends one only if it called
///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" />. Both
///         default to refusing. Read <c>docs/security-review.md</c> section 5a.
///     </para>
/// </remarks>
public sealed record FromSqlQueryRootStubNode : QueryRootStubNode
{
    /// <inheritdoc />
    public override NodeKind Kind => NodeKind.FromSqlQueryRootStub;

    /// <summary>
    ///     The caller's SQL text, verbatim.
    /// </summary>
    public required string Sql { get; init; }

    /// <summary>
    ///     The arguments EF collected for the SQL - <c>Expression.Constant(object?[])</c> on EF's
    ///     side, and translated here like any other constant.
    /// </summary>
    /// <remarks>
    ///     Carried as a node rather than as a value list so the values keep their types across the
    ///     wire: the server hands them back to EF, which binds them as <c>DbParameter</c>s. They
    ///     are never spliced into <see cref="Sql" /> - not because injection is the threat here (a
    ///     client that can send this node already writes whatever SQL it likes) but because a
    ///     value put into text has lost its type.
    /// </remarks>
    public required ExpressionNode Arguments { get; init; }
}
