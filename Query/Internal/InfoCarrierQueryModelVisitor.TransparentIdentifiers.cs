namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Remotion.Linq;
    using Remotion.Linq.Clauses;

    // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L1027
    public partial class InfoCarrierQueryModelVisitor
    {
        private const string CreateTransparentIdentifierMethodName = "CreateTransparentIdentifier";
        private int _transparentParameterCounter;

        private struct TransparentIdentifier<TOuter, TInner>
        {
            public static TransparentIdentifier<TOuter, TInner> CreateTransparentIdentifier(TOuter outer, TInner inner)
                => new TransparentIdentifier<TOuter, TInner>(outer, inner);

            private TransparentIdentifier(TOuter outer, TInner inner)
            {
                Outer = outer;
                Inner = inner;
            }

            public TOuter Outer;

            public TInner Inner;
        }

        private static Expression CallCreateTransparentIdentifier(
            Type transparentIdentifierType, Expression outerExpression, Expression innerExpression)
        {
            var createTransparentIdentifierMethodInfo
                = transparentIdentifierType.GetTypeInfo().GetDeclaredMethod(CreateTransparentIdentifierMethodName);

            return Expression.Call(createTransparentIdentifierMethodInfo, outerExpression, innerExpression);
        }

        private static Expression AccessOuterTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            var fieldInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Outer");

            return Expression.Field(targetExpression, fieldInfo);
        }

        private static Expression AccessInnerTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            var fieldInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Inner");

            return Expression.Field(targetExpression, fieldInfo);
        }

        private void IntroduceTransparentScope(
            IQuerySource fromClause, QueryModel queryModel, int index, Type transparentIdentifierType)
        {
            CurrentParameter
                = Expression.Parameter(transparentIdentifierType, $"t{_transparentParameterCounter++}");

            var outerAccessExpression
                = AccessOuterTransparentField(transparentIdentifierType, CurrentParameter);

            RescopeTransparentAccess(queryModel.MainFromClause, outerAccessExpression);

            for (var i = 0; i < index; i++)
            {
                var querySource = queryModel.BodyClauses[i] as IQuerySource;

                if (querySource != null)
                {
                    RescopeTransparentAccess(querySource, outerAccessExpression);
                }
            }

            AddOrUpdateMapping(fromClause, AccessInnerTransparentField(transparentIdentifierType, CurrentParameter));
        }

        private void RescopeTransparentAccess(IQuerySource querySource, Expression targetExpression)
        {
            var memberAccessExpression
                = ShiftMemberAccess(
                    targetExpression,
                    this.QueryCompilationContext.QuerySourceMapping.GetExpression(querySource));

            this.QueryCompilationContext.QuerySourceMapping.ReplaceMapping(querySource, memberAccessExpression);
        }

        private static Expression ShiftMemberAccess(Expression targetExpression, Expression currentExpression)
        {
            var memberExpression = currentExpression as MemberExpression;

            if (memberExpression == null)
            {
                return targetExpression;
            }

            return Expression.MakeMemberAccess(
                ShiftMemberAccess(targetExpression, memberExpression.Expression),
                memberExpression.Member);
        }
    }
}
