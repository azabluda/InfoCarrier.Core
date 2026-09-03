// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Metadata;

/// <summary>
///     Reads and rebuilds EF Core's two relational raw-SQL query roots —
///     <c>FromSqlQueryRootExpression</c> (#60) and <c>SqlQueryRootExpression</c> (#56).
/// </summary>
/// <remarks>
///     <para>
///         <b>A seam that survived the package it was cut for.</b> Both root types live in
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, which <c>InfoCarrier.Core</c> used not
///         to reference, so until #97 this was answered by reflection: two types resolved by full
///         name, four <c>GetProperty</c> reads and two <c>Activator.CreateInstance</c> calls under
///         ten trim suppressions. D3 is superseded since 2026-09-03 and the reference is now in
///         this assembly, so the interface no longer hides anything the caller could not name.
///         It stays because it is still the seam a non-relational backend would replace, and
///         because <c>ServerQueryExecutor</c> takes it as a parameter.
///     </para>
///     <para>
///         <b>There is one implementation and every client gets it.</b>
///         <see cref="InfoCarrier.Core.Relational.InfoCarrierRelationalQueryRoots" /> is registered
///         by <c>AddEntityFrameworkInfoCarrier</c> with no opt-in, because one package that carries
///         the relational half cannot save a consumer anything by withholding it. A client over a
///         non-relational backend never builds one of these roots, so the implementation answers
///         nothing there. <b>A raw-SQL root is never shipped with its SQL silently dropped</b>,
///         which is the defect R75 closed and the one this seam must not reopen.
///     </para>
///     <para>
///         <b>It does not decide whether raw SQL is permitted.</b> That is
///         <see cref="InfoCarrierDbContextOptionsBuilder.AllowArbitrarySqlExecution" /> on the
///         client and <see cref="IInfoCarrierArbitrarySqlExecution" /> on the server, and both
///         still default to refusing. This seam only knows the shape.
///     </para>
/// </remarks>
public interface IInfoCarrierRelationalQueryRoots
{
    /// <summary>
    ///     Whether a node is either of EF's relational raw-SQL query roots.
    /// </summary>
    /// <remarks>
    ///     One predicate for both, because one grant covers both. The implementation must match
    ///     the EXACT types and not a base: every query root carrying state beyond its entity type
    ///     is a subclass, and treating one as its base is what shipped a <c>FromSqlRaw</c> as the
    ///     whole table before R75.
    /// </remarks>
    bool IsRawSqlRoot(Expression node);

    /// <summary>
    ///     Reads the SQL and its argument off an <em>entity-typed</em> raw-SQL root
    ///     (<c>FromSql</c>).
    /// </summary>
    bool TryReadEntityRoot(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument);

    /// <summary>
    ///     Reads the SQL and its argument off a <em>scalar</em> raw-SQL root
    ///     (<c>Database.SqlQuery&lt;T&gt;</c> over a mapped scalar).
    /// </summary>
    bool TryReadScalarRoot(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument);

    /// <summary>
    ///     Rebuilds an entity-typed raw-SQL root against the <em>server's</em> entity type.
    /// </summary>
    Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument);

    /// <summary>
    ///     Rebuilds a scalar raw-SQL root from the element type the wire carried.
    /// </summary>
    /// <remarks>
    ///     A CLR <see cref="Type" /> and not an <see cref="IEntityType" />, because a scalar root
    ///     is the one query root with no entity type for a model to resolve.
    /// </remarks>
    Expression CreateScalarRoot(Type elementType, string sql, Expression argument);
}
