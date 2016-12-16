namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Internal;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Microsoft.EntityFrameworkCore.Storage;
    using Remotion.Linq.Clauses;
    using Remotion.Linq.Clauses.Expressions;

    public class InfoCarrierMemberAccessBindingExpressionVisitor : MemberAccessBindingExpressionVisitor
    {
        public InfoCarrierMemberAccessBindingExpressionVisitor(
            QuerySourceMapping querySourceMapping,
            EntityQueryModelVisitor queryModelVisitor,
            bool inProjection)
            : base(querySourceMapping, queryModelVisitor, inProjection)
        {
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (EntityQueryModelVisitor.IsPropertyMethod(methodCallExpression.Method)
                && methodCallExpression.Arguments[0] is QuerySourceReferenceExpression)
            {
                const string callerName = nameof(this.VisitMethodCall);
                Expression arg0 = this.VisitAndConvert(methodCallExpression.Arguments[0].RemoveConvert(), callerName);
                Expression arg1 = this.VisitAndConvert(methodCallExpression.Arguments[1], callerName);

                // Compensate for ValueBuffer being a struct, and hence not compatible with Object method
                arg0 = arg0.Type == typeof(ValueBuffer)
                    ? Expression.Convert(arg0, typeof(object))
                    : arg0;

                return Expression.Call(methodCallExpression.Method, arg0, arg1);
            }

            return base.VisitMethodCall(methodCallExpression);
        }
    }
}
