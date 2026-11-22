// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using InfoCarrier.Core.Metadata;
using Microsoft.EntityFrameworkCore.Metadata;
using CosmosRoot = Microsoft.EntityFrameworkCore.Cosmos.Query.Internal.FromSqlQueryRootExpression;

namespace InfoCarrier.Core.CosmosTests.Specifications.CosmosSql;

#pragma warning disable EF1001 // The public query-root seam adapts this exact official provider root type.
internal sealed class CosmosSqlQueryRoots : IInfoCarrierRelationalQueryRoots
{
    public bool IsRawSqlRoot(Expression node) => node.GetType() == typeof(CosmosRoot);
    public bool TryReadEntityRoot(Expression node, [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        if (IsRawSqlRoot(node) && node is CosmosRoot root)
        {
            sql = root.Sql;
            argument = root.Argument;
            return true;
        }
        sql = null;
        argument = null;
        return false;
    }
    public bool TryReadScalarRoot(Expression node, [NotNullWhen(true)] out string? sql,
        [NotNullWhen(true)] out Expression? argument)
    {
        sql = null;
        argument = null;
        return false;
    }
    public Expression CreateEntityRoot(IEntityType entityType, string sql, Expression argument)
        => new CosmosRoot(entityType, sql, argument);
    public Expression CreateScalarRoot(Type elementType, string sql, Expression argument)
        => throw new NotSupportedException("This Cosmos provider exposes entity FromSql roots only.");
}
