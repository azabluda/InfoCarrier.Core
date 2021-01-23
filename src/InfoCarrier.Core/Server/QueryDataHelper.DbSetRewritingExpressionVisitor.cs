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

            protected override Expression VisitConstant(ConstantExpression node)
                => node.Type.IsGenericType && node.Type.GetGenericTypeDefinition() == typeof(InternalDbSet<>)
                ? new QueryRootExpression(this.provider, this.model.FindRuntimeEntityType(node.Type.GetGenericArguments()[0]))
                : base.VisitConstant(node);
        }
    }
}
