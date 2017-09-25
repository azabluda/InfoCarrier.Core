namespace InfoCarrier.Core.Client.Query.Internal
{
    using System.Linq.Expressions;
    using Microsoft.EntityFrameworkCore.Query.Internal;

    public class InfoCarrierEvaluatableExpressionFilter : EvaluatableExpressionFilter
    {
        public override bool IsEvaluatableMember(MemberExpression memberExpression)
            => Remote.Linq.EntityFrameworkCore.ExpressionEvaluator.CanBeEvaluated(memberExpression)
               && base.IsEvaluatableMember(memberExpression);
    }
}
