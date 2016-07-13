namespace InfoCarrier.Core.Client.Query.Internal
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using Remotion.Linq;
    using Remotion.Linq.Clauses;
    using Utils;

    // https://github.com/aspnet/EntityFramework/blob/1.0.0/src/Microsoft.EntityFrameworkCore/Query/EntityQueryModelVisitor.cs#L1027
    public partial class InfoCarrierQueryModelVisitor
    {
        private int _transparentParameterCounter;

        private Type GetTransparentIdentifierType(Type outer, Type inner)
        {
            return SymbolExtensions.GetMethodInfo(() => GetTransparentIdentifierType<object, object>())
                .GetGenericMethodDefinition()
                .MakeGenericMethod(outer, inner)
                .ToDelegate<Func<Type>>()
                .Invoke();
        }

        private static Type GetTransparentIdentifierType<TOuter, TInner>()
        {
            var transparentIdentifier = new {Outer = default(TOuter), Inner = default(TInner)};
            return transparentIdentifier.GetType();
        }

        private static Expression CallCreateTransparentIdentifier(
            Type transparentIdentifierType, Expression outerExpression, Expression innerExpression)
        {
            ConstructorInfo ctor = transparentIdentifierType.GetTypeInfo().DeclaredConstructors.Single();
            return Expression.New(
                ctor,
                new[] {outerExpression, innerExpression},
                transparentIdentifierType.GetTypeInfo().GetDeclaredProperty("Outer"),
                transparentIdentifierType.GetTypeInfo().GetDeclaredProperty("Inner"));
        }

        private static Expression AccessOuterTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            var propertyInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredProperty("Outer");

            return Expression.Property(targetExpression, propertyInfo);
        }

        private static Expression AccessInnerTransparentField(
            Type transparentIdentifierType, Expression targetExpression)
        {
            var propertyInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredProperty("Inner");

            return Expression.Property(targetExpression, propertyInfo);
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
