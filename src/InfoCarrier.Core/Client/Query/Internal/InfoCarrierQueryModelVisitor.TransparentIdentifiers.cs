namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Common;
    using Remotion.Linq;
    using Remotion.Linq.Clauses;

    // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L1027
    public partial class InfoCarrierQueryModelVisitor
    {
        private static readonly MethodInfo GetTransparentIdentifierTypeMethod
            = Utils.GetMethodInfo(() => GetTransparentIdentifierType<object, object>())
                .GetGenericMethodDefinition();

        private int transparentParameterCounter;

        private static Type GetTransparentIdentifierType(Type outer, Type inner)
        {
            return GetTransparentIdentifierTypeMethod
                .MakeGenericMethod(outer, inner)
                .ToDelegate<Func<Type>>()
                .Invoke();
        }

        private static Type GetTransparentIdentifierType<TOuter, TInner>()
        {
            var transparentIdentifier = new { Outer = default(TOuter), Inner = default(TInner) };
            return transparentIdentifier.GetType();
        }

        protected override Expression CallCreateTransparentIdentifier(
            Type transparentIdentifierType, Expression outerExpression, Expression innerExpression)
        {
            ConstructorInfo ctor = transparentIdentifierType.GetTypeInfo().DeclaredConstructors.Single();
            return Expression.New(
                ctor,
                new[] { outerExpression, innerExpression },
                transparentIdentifierType.GetTypeInfo().GetDeclaredProperty(@"Outer"),
                transparentIdentifierType.GetTypeInfo().GetDeclaredProperty(@"Inner"));
        }

        private static Expression AccessOuterTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            PropertyInfo propertyInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredProperty(@"Outer");

            return Expression.Property(targetExpression, propertyInfo);
        }

        private static Expression AccessInnerTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            PropertyInfo propertyInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredProperty(@"Inner");

            return Expression.Property(targetExpression, propertyInfo);
        }

        protected override void IntroduceTransparentScope(
            IQuerySource querySource, QueryModel queryModel, int index, Type transparentIdentifierType)
        {
            this.CurrentParameter
                = Expression.Parameter(
                    transparentIdentifierType,
                    string.Format(CultureInfo.InvariantCulture, "t{0}", this.transparentParameterCounter++));

            var outerAccessExpression
                = AccessOuterTransparentField(transparentIdentifierType, this.CurrentParameter);

            this.RescopeTransparentAccess(queryModel.MainFromClause, outerAccessExpression);

            for (var i = 0; i < index; i++)
            {
                var bodyClause = queryModel.BodyClauses[i] as IQuerySource;

                if (bodyClause != null)
                {
                    this.RescopeTransparentAccess(bodyClause, outerAccessExpression);

                    var groupJoinClause = bodyClause as GroupJoinClause;

                    if (groupJoinClause != null
                        && this.QueryCompilationContext.QuerySourceMapping
                            .ContainsMapping(groupJoinClause.JoinClause))
                    {
                        this.RescopeTransparentAccess(groupJoinClause.JoinClause, outerAccessExpression);
                    }
                }
            }

            this.QueryCompilationContext.AddOrUpdateMapping(querySource, AccessInnerTransparentField(transparentIdentifierType, this.CurrentParameter));
        }

        private void RescopeTransparentAccess(IQuerySource querySource, Expression targetExpression)
        {
            Expression memberAccessExpression
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

            try
            {
                return Expression.MakeMemberAccess(
                    ShiftMemberAccess(targetExpression, memberExpression.Expression),
                    memberExpression.Member);
            }
            catch (ArgumentException)
            {
                // Member is not defined on the new target expression.
                // This is due to stale QuerySourceMappings, which we can't
                // remove due to there not being an API on QuerySourceMapping.
            }

            return currentExpression;
        }
    }
}
