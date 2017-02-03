namespace InfoCarrier.Core.Client.Query.ExpressionVisitors.Internal
{
    using System.Collections.ObjectModel;
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
    using Remotion.Linq.Clauses;

    public class InfoCarrierMemberAccessBindingExpressionVisitor : MemberAccessBindingExpressionVisitor
    {
        public InfoCarrierMemberAccessBindingExpressionVisitor(
            QuerySourceMapping querySourceMapping,
            EntityQueryModelVisitor queryModelVisitor,
            bool inProjection)
            : base(querySourceMapping, queryModelVisitor, inProjection)
        {
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            // Basically we just want to call base.base.VisitMethodCall(expression) here
            // bypassing the MemberAccessBindingExpressionVisitor.VisitMethodCall logic,
            // but C# doesn't allow it.

            // UGLY: copy-n-paste of https://github.com/re-motion/Relinq/blob/v2.1.1/Core.Net_3_5/Parsing/ExpressionVisitor.cs#L252
            Expression newObject = this.Visit(expression.Object);
            ReadOnlyCollection<Expression> newArguments = this.VisitAndConvert(expression.Arguments, nameof(this.VisitMethodCall));
            if ((newObject != expression.Object) || (newArguments != expression.Arguments))
            {
                return Expression.Call(newObject, expression.Method, newArguments);
            }

            return expression;
        }
    }
}
