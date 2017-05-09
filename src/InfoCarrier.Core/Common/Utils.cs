namespace InfoCarrier.Core.Common
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
    using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;

    public static class Utils
    {
        public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
            => GetMethodInfo(expression.Body);

        public static MethodInfo GetMethodInfo(Expression<Action> expression)
            => GetMethodInfo(expression.Body);

        private static MethodInfo GetMethodInfo(Expression expressionBody)
        {
            var outermostExpression = expressionBody as MethodCallExpression;
            if (outermostExpression != null)
            {
                return outermostExpression.Method;
            }

            throw new ArgumentException(@"Invalid Expression. Expression should consist of a Method call only.");
        }

        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo)
        {
            return (TDelegate)(object)methodInfo.CreateDelegate(typeof(TDelegate));
        }

        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo, object firstArgument)
        {
            return (TDelegate)(object)methodInfo.CreateDelegate(typeof(TDelegate), firstArgument);
        }

        internal static Type TryGetQueryResultSequenceType(Type type)
        {
            // Despite formally a string is a sequence of chars, we treat it as a scalar type
            if (type == typeof(string))
            {
                return null;
            }

            // Arrays is another special case
            if (type.IsArray)
            {
                return null;
            }

            // Grouping is another special case
            if (type.IsGrouping())
            {
                return null;
            }

            return type.TryGetSequenceType();
        }

        internal static Expression ReplaceNullConditional(Expression expression, bool toStub)
        {
            return new ReplaceNullConditionalExpressionVisitor(toStub).Visit(expression);
        }

        private class ReplaceNullConditionalExpressionVisitor : ExpressionVisitorBase
        {
            private readonly bool toStub;

            private static readonly MethodInfo NullConditionalExpressionStubMethod
                = GetMethodInfo(() => NullConditionalExpressionStub<object, object, object, object>(null, null, null))
                    .GetGenericMethodDefinition();

            public ReplaceNullConditionalExpressionVisitor(bool toStub)
            {
                this.toStub = toStub;
            }

            protected override Expression VisitExtension(Expression node)
            {
                if (this.toStub)
                {
                    if (node is NullConditionalExpression nullConditionalExpression)
                    {
                        return Expression.Call(
                            null,
                            NullConditionalExpressionStubMethod.MakeGenericMethod(
                                nullConditionalExpression.NullableCaller.Type,
                                nullConditionalExpression.Caller.Type,
                                nullConditionalExpression.AccessOperation.Type,
                                node.Type),
                            this.Visit(nullConditionalExpression.NullableCaller),
                            this.Visit(nullConditionalExpression.Caller),
                            this.Visit(nullConditionalExpression.AccessOperation));
                    }
                }

                return base.VisitExtension(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (!this.toStub)
                {
                    if (node.Method.MethodIsClosedFormOf(NullConditionalExpressionStubMethod))
                    {
                        return new NullConditionalExpression(
                            this.Visit(node.Arguments[0]),
                            this.Visit(node.Arguments[1]),
                            this.Visit(node.Arguments[2]));
                    }
                }

                return base.VisitMethodCall(node);
            }

            public static TResult NullConditionalExpressionStub<T1, T2, T3, TResult>(T1 nullableCaller, T2 caller, T3 accessOperation)
            {
                throw new InvalidOperationException("The NullConditionalExpressionStub&lt;T&gt; method may only be used within LINQ queries.");
            }
        }
    }
}
