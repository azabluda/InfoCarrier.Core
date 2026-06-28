// Copyright (c) Alexander Zabluda. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Server
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query;

    /// <summary>
    /// Rewrite DbSet constant expression with QueryRootExpression.
    /// </summary>
    internal partial class QueryDataHelper
    {
        private class DbSetRewritingExpressionVisitor : ExpressionVisitor
        {
            private readonly IModel model;
            private readonly IAsyncQueryProvider provider;

            public DbSetRewritingExpressionVisitor(DbContext dbContext)
            {
                this.model = dbContext.Model;
                this.provider = dbContext.GetService<IAsyncQueryProvider>();
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                if (node.NodeType == ExpressionType.Convert
                    && node.Operand is UnaryExpression inner
                    && inner.NodeType == ExpressionType.Convert
                    && node.Type == inner.Type)
                {
                    return this.Visit(inner);
                }

                return base.VisitUnary(node);
            }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Type.IsGenericType
                    && node.Type.GetGenericTypeDefinition() == typeof(InternalDbSet<>))
                {
                    var clrType = node.Type.GetGenericArguments()[0];
                    var entityType = this.model.FindRuntimeEntityType(clrType)
                        ?? this.model.FindEntityType(clrType);

                    // For shared type entities, FindRuntimeEntityType/FindEntityType
                    // may return null. Scan all entity types for a CLR type match.
                    if (entityType == null)
                    {
                        foreach (var et in this.model.GetEntityTypes())
                        {
                            if (et.ClrType == clrType)
                            {
                                entityType = et;
                                break;
                            }
                        }
                    }

                    if (entityType != null)
                    {
                        return new QueryRootExpression(this.provider, entityType);
                    }
                }

                return base.VisitConstant(node);
            }
        }
    }
}
