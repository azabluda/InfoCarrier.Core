// Copyright (c) on/off it-solutions gmbh. All rights reserved.
// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Common
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using InfoCarrier.Core.Properties;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Extensions.Internal;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Query.Expressions.Internal;
    using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

    /// <summary>
    ///     A collection of miscellaneous helper functions.
    /// </summary>
    public static class Utils
    {
        // ReSharper disable once InvokeAsExtensionMethod
        private static readonly ImmutableHashSet<MethodInfo> SingleResultMethods =
            Enumerable.Concat(
                typeof(Queryable).GetMethods().Where(
                    m => !m.ReturnType.IsGenericType ||
                    !new[] { typeof(IQueryable<>), typeof(IOrderedQueryable<>) }
                        .Contains(m.ReturnType.GetGenericTypeDefinition())),
                typeof(Enumerable).GetMethods().Where(
                    m => !m.ReturnType.IsGenericType ||
                    !new[] { typeof(IEnumerable<>), typeof(IOrderedEnumerable<>) }
                        .Contains(m.ReturnType.GetGenericTypeDefinition())))
            .ToImmutableHashSet();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        internal static Type TryGetSequenceType(Type type)
        {
            var types = GetGenericTypeImplementations(type, typeof(IEnumerable<>));
            return types.SingleOrDefault()?.GetTypeInfo().GenericTypeArguments.FirstOrDefault();
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:ElementParametersMustBeDocumented", Justification = "Entity Framework Core internal.")]
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:ElementReturnValueMustBeDocumented", Justification = "Entity Framework Core internal.")]
        internal static IEnumerable<Type> GetGenericTypeImplementations(Type type, Type interfaceOrBaseType)
        {
            var typeInfo = type.GetTypeInfo();
            if (!typeInfo.IsGenericTypeDefinition)
            {
                foreach (var baseType in typeInfo.ImplementedInterfaces)
                {
                    if (baseType.GetTypeInfo().IsGenericType
                        && baseType.GetGenericTypeDefinition() == interfaceOrBaseType)
                    {
                        yield return baseType;
                    }
                }

                if (type.GetTypeInfo().IsGenericType
                    && type.GetGenericTypeDefinition() == interfaceOrBaseType)
                {
                    yield return type;
                }
            }
        }

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

            throw new ArgumentException(InfoCarrierStrings.InvalidMethodCallExpression(expressionBody));
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
        ///     Checks whether the given query returns single result.
        /// </summary>
        /// <param name="query"> The query to inspect. </param>
        /// <returns> True if the given query returns single result. </returns>
        internal static bool QueryReturnsSingleResult(Expression query)
        {
            if (query is MethodCallExpression methodCall)
            {
                MethodInfo method = methodCall.Method;
                if (method.IsGenericMethod)
                {
                    method = method.GetGenericMethodDefinition();
                }

                return SingleResultMethods.Contains(method);
            }

            return false;
        }

        /// <summary>
        ///     Converts the given store value to its model counterpart if there is a <see cref="ValueConverter" />
        ///     defined for the given <paramref name="property"/>.
        /// </summary>
        /// <remarks>
        ///     If the <paramref name="property"/> is null or defines no <see cref="ValueConverter" /> then
        ///     no conversion is applied to the <paramref name="value"/>.
        /// </remarks>
        /// <param name="value"> The value to convert. </param>
        /// <param name="property"> The property metadata which may define a converter. </param>
        /// <returns> The converted value. </returns>
        internal static object ConvertFromProvider(object value, IProperty property)
        {
            ValueConverter valueConverter = property?.GetValueConverter();
            return valueConverter != null ? valueConverter.ConvertFromProvider(value) : value;
        }

        /// <summary>
        ///     Converts the given model value to its store counterpart if there is a <see cref="ValueConverter" />
        ///     defined for the given <paramref name="property"/>.
        /// </summary>
        /// <remarks>
        ///     If the <paramref name="property"/> is null or defines no <see cref="ValueConverter" /> then
        ///     no conversion is applied to the <paramref name="value"/>.
        /// </remarks>
        /// <param name="value"> The value to convert. </param>
        /// <param name="property"> The property metadata which may define a converter. </param>
        /// <returns> The converted value. </returns>
        internal static object ConvertToProvider(object value, IProperty property)
        {
            ValueConverter valueConverter = property?.GetValueConverter();
            return valueConverter != null ? valueConverter.ConvertToProvider(value) : value;
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
        public static Expression ReplaceNullConditional(Expression expression, bool toStub)
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

            [ExcludeFromCoverage]
            public static TResult NullConditionalExpressionStub<T1, T2, TResult>(T1 caller, T2 accessOperation)
            {
                throw new InvalidOperationException(InfoCarrierStrings.NullConditionalExpressionStubMethodInvoked);
            }
        }
    }
}
