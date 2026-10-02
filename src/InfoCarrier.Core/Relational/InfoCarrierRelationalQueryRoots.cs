// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
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
///         <b>This class is the whole argument for referencing the relational package.</b> The
///         implementation it replaces did the same job with reflection, because
///         <c>InfoCarrier.Core</c> could not then name
///         <c>Microsoft.EntityFrameworkCore.Relational</c>: two types resolved by full name
///         against every loaded assembly, four <c>GetProperty</c> reads, two
///         <c>Activator.CreateInstance</c> calls, and <b>ten
///         <c>UnconditionalSuppressMessage</c> attributes</b> arguing that the members survive
///         trimming. Typed calls replace that reflection, and the compiler checks them.
///     </para>
///     <para>
///         <b>A non-relational backend stays possible, and nothing here is what would stop it.</b>
///         This type is asked whether a node is one of EF's raw-SQL query roots, and a query that
///         holds none never reaches it. See
///         <c>InfoCarrierDbContextOptionsBuilder.UseNonRelationalServerStore</c> for the rules a
///         client does relax when the server's store is not a database of tables.
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
    /// <summary>
    ///     The shared instance. This type has no state, so every caller can have the same object.
    /// </summary>
    public static readonly InfoCarrierRelationalQueryRoots Instance = new();

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
    ///     builds beside it. A constant argument array keeps the client's constantization decision
    ///     through the server's parameter extraction; other argument expressions use EF's root.
    /// </remarks>
    public Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument)
        => argument is ConstantExpression { Value: object[] } constant
            ? new ConstantArgumentsFromSqlQueryRootExpression(entityType, sql, constant)
            : new FromSqlQueryRootExpression(entityType, sql, argument);

    /// <inheritdoc />
    public Expression CreateScalarRoot(Type elementType, string sql, Expression argument)
        => new SqlQueryRootExpression(elementType, sql, argument);

    /// <summary>
    ///     Keeps arguments that EF already left constant from becoming parameters on the server.
    /// </summary>
    /// <remarks>
    ///     Compiled queries leave the raw SQL argument array constant. Ordinary queries extract it
    ///     as a parameter, which arrives as a member read and never uses this root. The server runs
    ///     a normal query, whose funcletizer would otherwise parameterize this constant array too.
    ///     Protection survives server-side visitors before extraction. EF's invocation-removal
    ///     visitor starts query preprocessing after extraction and restores the ordinary raw root.
    ///     EF still formats each literal through the store's type mapping, including quote escaping.
    /// </remarks>
    private sealed class ConstantArgumentsFromSqlQueryRootExpression(
        IEntityType entityType, string sql, ConstantExpression argument)
        : EntityQueryRootExpression(entityType)
    {
        private readonly FromSqlQueryRootExpression _root = new(entityType, sql, argument);

        public override Expression DetachQueryProvider()
            => this;

        public override EntityQueryRootExpression UpdateEntityType(IEntityType newEntityType)
        {
            var updated = (FromSqlQueryRootExpression)_root.UpdateEntityType(newEntityType);
            return new ConstantArgumentsFromSqlQueryRootExpression(
                updated.EntityType, updated.Sql, (ConstantExpression)updated.Argument);
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
            => visitor is InvocationExpressionRemovingExpressionVisitor ? visitor.Visit(_root) : this;

        protected override void Print(ExpressionPrinter expressionPrinter)
            => expressionPrinter.Visit(_root);

        public override bool Equals(object? obj)
            => obj is ConstantArgumentsFromSqlQueryRootExpression other && _root.Equals(other._root);

        public override int GetHashCode()
            => _root.GetHashCode();
    }
}
