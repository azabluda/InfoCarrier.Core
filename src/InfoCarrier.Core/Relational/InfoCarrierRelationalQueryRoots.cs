// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;

// Internal EF Core API usage. This provider is built on EF Core internals by design (CLAUDE.md),
// and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.Relational;

/// <summary>
///     Reads and rebuilds EF Core's relational raw-SQL query roots by naming them.
/// </summary>
/// <remarks>
///     <para>
///         <b>This class is the whole argument for the package.</b> The implementation it replaces
///         did the same job with reflection, because <c>InfoCarrier.Core</c> may not reference
///         <c>Microsoft.EntityFrameworkCore.Relational</c> (<c>architecture.md</c> §6a <b>D3</b>):
///         two types resolved by full name against every loaded assembly, four
///         <c>GetProperty</c> reads, two <c>Activator.CreateInstance</c> calls, and <b>ten
///         <c>UnconditionalSuppressMessage</c> attributes</b> arguing that the members survive
///         trimming. All of it is gone; what is left is the four lines a compiler can check.
///     </para>
///     <para>
///         <b>D3 is untouched.</b> The reference lives here, in a package an application adds only
///         when its backing store is relational, so a non-relational backend stays possible.
///     </para>
///     <para>
///         <b>The exact types, not a base.</b> Every query root that carries state beyond its
///         entity type is a subclass of <c>QueryRootExpression</c>, and treating one as its base is
///         what shipped a <c>FromSqlRaw</c> as the whole table before R75. Naming the types makes
///         that a compile-time guarantee rather than a string comparison.
///     </para>
/// </remarks>
public sealed class InfoCarrierRelationalQueryRoots : IInfoCarrierRelationalQueryRoots
{
    /// <inheritdoc />
    public bool IsRawSqlRoot(Expression node)
        => node is FromSqlQueryRootExpression or SqlQueryRootExpression;

    /// <inheritdoc />
    public bool TryReadEntityRoot(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        if (node is FromSqlQueryRootExpression root)
        {
            sql = root.Sql;
            argument = root.Argument;
            return true;
        }

        sql = null;
        argument = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryReadScalarRoot(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        if (node is SqlQueryRootExpression root)
        {
            sql = root.Sql;
            argument = root.Argument;
            return true;
        }

        sql = null;
        argument = null;
        return false;
    }

    /// <inheritdoc />
    /// <remarks>
    ///     The three-argument constructor, which is the one EF's own <c>DetachQueryProvider</c> and
    ///     <c>UpdateEntityType</c> use: a root rebuilt here has no query provider, exactly like the
    ///     plain <c>EntityQueryRootExpression</c> that <c>ServerQueryExecutor.RebindQueryRoot</c>
    ///     builds beside it.
    /// </remarks>
    public Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument)
        => new FromSqlQueryRootExpression(entityType, sql, argument);

    /// <inheritdoc />
    public Expression CreateScalarRoot(Type elementType, string sql, Expression argument)
        => new SqlQueryRootExpression(elementType, sql, argument);
}
