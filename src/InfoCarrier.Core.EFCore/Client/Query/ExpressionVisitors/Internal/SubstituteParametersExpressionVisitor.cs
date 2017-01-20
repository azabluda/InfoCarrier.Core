namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Remote.Linq;

    internal class SubstituteParametersExpressionVisitor : ExpressionVisitorBase
    {
        private readonly QueryContext queryContext;

        public SubstituteParametersExpressionVisitor(QueryContext queryContext)
        {
            this.queryContext = queryContext;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.MethodIsClosedFormOf(DefaultQueryExpressionVisitor.GetParameterValueMethodInfo))
            {
                Type paramType = node.Method.GetGenericArguments().Single();
                object paramValue =
                    InfoCarrier.Core.MethodInfoExtensions.GetMethodInfo(() => this.GetParameterValue<object>(node))
                        .GetGenericMethodDefinition()
                        .MakeGenericMethod(paramType)
                        .ToDelegate<Func<MethodCallExpression, object>>(this)
                        .Invoke(node);

                return Expression.Property(
                    Expression.Constant(paramValue),
                    paramValue.GetType(),
                    nameof(VariableQueryArgument<object>.Value));
            }

            return base.VisitMethodCall(node);
        }

        private object GetParameterValue<T>(MethodCallExpression node) =>
            new VariableQueryArgument<T>(
                Expression
                    .Lambda<Func<QueryContext, T>>(node, EntityQueryModelVisitor.QueryContextParameter)
                    .Compile()
                    .Invoke(this.queryContext));
    }
}
