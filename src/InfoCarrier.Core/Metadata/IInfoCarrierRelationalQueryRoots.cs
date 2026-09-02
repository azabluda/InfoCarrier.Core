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
///         <b>A seam because <c>InfoCarrier.Core</c> cannot name those types.</b> Both live in
///         <c>Microsoft.EntityFrameworkCore.Relational</c>, which this package deliberately does
///         not reference (<c>architecture.md</c> §6a <b>D3</b>, M9 J5). Until #97 this was answered
///         by reflection — two types resolved by full name, four <c>GetProperty</c> reads and two
///         <c>Activator.CreateInstance</c> calls under ten trim suppressions. The implementation
///         now lives in <c>InfoCarrier.Core.Relational</c>, which references the package and names
///         the types outright, and <b>D3 still stands</b>: the reference is outside this assembly.
///     </para>
///     <para>
///         <b>Absent by default, and the default is a refusal rather than a hole.</b>
///         <see cref="NoRelationalQueryRoots" /> is registered by
///         <c>AddEntityFrameworkInfoCarrier</c>; it reports every node as not-a-raw-SQL-root, so
///         <c>ServerBoundaryAnalyzer</c> falls through to its <c>QueryRootExpression =&gt; false</c>
///         catch-all and the caller gets EF's own <c>TranslationFailed</c>. <b>A raw-SQL root is
///         never shipped with its SQL silently dropped</b>, which is the defect R75 closed and the
///         one this seam must not reopen.
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

/// <summary>
///     The default <see cref="IInfoCarrierRelationalQueryRoots" />: this half knows nothing about a
///     relational store, so it recognises no raw-SQL root and rebuilds none.
/// </summary>
/// <remarks>
///     Registered by <c>AddEntityFrameworkInfoCarrier</c> and replaced by
///     <c>AddInfoCarrierRelational()</c> from the <c>InfoCarrier.Core.Relational</c> package. The
///     same arrangement <see cref="AnnotationDocumentMapping" /> has: a default that is honest
///     about knowing nothing, and an application that supplies the store's answer.
/// </remarks>
public sealed class NoRelationalQueryRoots : IInfoCarrierRelationalQueryRoots
{
    /// <summary>
    ///     The shared instance. This type has no state, so every caller wanting "nothing is
    ///     relational here" can have the same object.
    /// </summary>
    public static readonly NoRelationalQueryRoots Instance = new();

    /// <inheritdoc />
    public bool IsRawSqlRoot(Expression node)
        => false;

    /// <inheritdoc />
    public bool TryReadEntityRoot(
        Expression node,
        [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
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
        sql = null;
        argument = null;
        return false;
    }

    /// <inheritdoc />
    public Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument)
        => throw NotRelational();

    /// <inheritdoc />
    public Expression CreateScalarRoot(Type elementType, string sql, Expression argument)
        => throw NotRelational();

    private static InvalidOperationException NotRelational()
        => new(
            "A raw-SQL query root reached this half, which does not know how to rebuild one. "
            + "Reference the InfoCarrier.Core.Relational package and call "
            + "services.AddInfoCarrierRelational() on both the client and the server when the "
            + "backing store is relational.");
}
