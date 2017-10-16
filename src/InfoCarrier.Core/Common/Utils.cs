﻿// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
    using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;

    /// <summary>
    ///     A collection of miscellaneous helper functions.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        ///     Given a lambda expression that calls a method, returns the <see cref="MethodInfo"/>.
        /// </summary>
        /// <typeparam name="T">The return type of the method.</typeparam>
        /// <param name="expression">The lambda expression.</param>
        /// <returns>Method metadata.</returns>
        public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
            => GetMethodInfo(expression.Body);

        /// <summary>
        ///     Given a lambda expression that calls a method, returns the <see cref="MethodInfo"/>.
        /// </summary>
        /// <param name="expression">The lambda expression.</param>
        /// <returns>Method metadata.</returns>
        public static MethodInfo GetMethodInfo(Expression<Action> expression)
            => GetMethodInfo(expression.Body);

        private static MethodInfo GetMethodInfo(Expression expressionBody)
        {
            if (expressionBody is MethodCallExpression outermostExpression)
            {
                return outermostExpression.Method;
            }

            throw new ArgumentException(@"Invalid Expression. Expression should consist of a Method call only.");
        }

        /// <summary>
        ///     Converts the given <see cref="MethodInfo"/> to a strongly typed invokable delegate.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate.</typeparam>
        /// <param name="methodInfo">Method metadata.</param>
        /// <returns>The delegate for this method.</returns>
        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo)
        {
            return (TDelegate)(object)methodInfo.CreateDelegate(typeof(TDelegate));
        }

        /// <summary>
        ///     Converts the given <see cref="MethodInfo"/> to a strongly typed invokable delegate.
        /// </summary>
        /// <typeparam name="TDelegate">The type of the delegate.</typeparam>
        /// <param name="methodInfo">Method metadata.</param>
        /// <param name="target">The object targeted by the delegate.</param>
        /// <returns>The delegate for this method.</returns>
        public static TDelegate ToDelegate<TDelegate>(this MethodInfo methodInfo, object target)
        {
            return (TDelegate)(object)methodInfo.CreateDelegate(typeof(TDelegate), target);
        }

        /// <summary>
        ///     Tries to guess the element type if the given type is a sequence.
        /// </summary>
        /// <param name="queryResultType">The result type of a Linq query.</param>
        /// <returns>Guessed sequence element type or null.</returns>
        internal static Type TryGetQueryResultSequenceType(Type queryResultType)
        {
            // Despite formally a string is a sequence of chars, we treat it as a scalar type
            if (queryResultType == typeof(string))
            {
                return null;
            }

            // Arrays is another special case
            if (queryResultType.IsArray)
            {
                return null;
            }

            // Grouping is another special case
            if (queryResultType.IsGrouping())
            {
                return null;
            }

            return queryResultType.TryGetSequenceType();
        }

        /// <summary>
        ///     Replace <see cref="NullConditionalExpression" /> nodes of the given expression tree with
        ///     NullConditionalExpressionStub method calls to make the expression ready for translation
        ///     to a serializable <see cref="Remote.Linq.Expressions.Expression"/>, and vice versa.
        /// </summary>
        /// <param name="expression">The original expression tree.</param>
        /// <param name="toStub">
        ///     <value>true</value> to replace <see cref="NullConditionalExpression" />'s with stubs.
        ///     <value>false</value> to replace stubs with <see cref="NullConditionalExpression" />'s.
        /// </param>
        /// <returns>
        ///     The new expression tree with replaced <see cref="NullConditionalExpression" /> nodes.
        /// </returns>
        internal static Expression ReplaceNullConditional(Expression expression, bool toStub)
        {
            return new ReplaceNullConditionalExpressionVisitor(toStub).Visit(expression);
        }

        private class ReplaceNullConditionalExpressionVisitor : ExpressionVisitorBase
        {
            private readonly bool toStub;

            private static readonly MethodInfo NullConditionalExpressionStubMethod
                = GetMethodInfo(() => NullConditionalExpressionStub<object, object, object>(null, null))
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
                                nullConditionalExpression.Caller.Type,
                                nullConditionalExpression.AccessOperation.Type,
                                node.Type),
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
                            this.Visit(node.Arguments[1]));
                    }
                }

                return base.VisitMethodCall(node);
            }

            public static TResult NullConditionalExpressionStub<T1, T2, TResult>(T1 caller, T2 accessOperation)
            {
                throw new InvalidOperationException("The NullConditionalExpressionStub&lt;T&gt; method may only be used within LINQ queries.");
            }
        }
    }
}
