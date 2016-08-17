namespace InfoCarrier.Core.Client.Query.ExpressionVisitors
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Utils;

    internal class SubstituteParametersExpressionVisitor : ExpressionVisitorBase
    {
        private readonly QueryContext queryContext;

        public SubstituteParametersExpressionVisitor(QueryContext queryContext)
        {
            this.queryContext = queryContext;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (MethodInfoExtensions.MethodIsClosedFormOf(node.Method, DefaultQueryExpressionVisitor.GetParameterValueMethodInfo))
            {
                Type paramType = node.Method.GetGenericArguments().Single();
                object paramValue =
                    SymbolExtensions.GetMethodInfo(() => this.GetParameterValue<object>(node))
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(paramType)
                        .ToDelegate<Func<MethodCallExpression, object>>(this)
                        .Invoke(node);
                return Expression.Constant(paramValue, paramType);
            }

            return base.VisitMethodCall(node);
        }

        private object GetParameterValue<T>(MethodCallExpression node) =>
            Expression
                .Lambda<Func<QueryContext, T>>(node, EntityQueryModelVisitor.QueryContextParameter)
                .Compile()
                .Invoke(this.queryContext);
    }
}
