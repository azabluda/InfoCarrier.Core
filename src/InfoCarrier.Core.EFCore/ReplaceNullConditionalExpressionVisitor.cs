namespace InfoCarrier.Core
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;

    internal class ReplaceNullConditionalExpressionVisitor : ExpressionVisitorBase
    {
        private readonly bool toMethodCall;

        private static readonly MethodInfo NullConditionalExpressionStubMethod
            = MethodInfoExtensions.GetMethodInfo(() => NullConditionalExpressionStub<object, object, object, object>(null, null, null))
                .GetGenericMethodDefinition();

        public ReplaceNullConditionalExpressionVisitor(bool toMethodCall)
        {
            this.toMethodCall = toMethodCall;
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (this.toMethodCall)
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
            if (!this.toMethodCall)
            {
                if (node.Method.IsGenericMethod
                    && node.Method.GetGenericMethodDefinition() == NullConditionalExpressionStubMethod)
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
