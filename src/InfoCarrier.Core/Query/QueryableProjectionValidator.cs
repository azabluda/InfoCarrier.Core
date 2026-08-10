// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Text;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Refuses a projection that is still a query — an <c>IQueryable&lt;T&gt;</c> or an
///     <c>IOrderedEnumerable&lt;T&gt;</c> in the result — as EF does.
/// </summary>
/// <remarks>
///     <para>
///         EF raises this in <c>QueryableMethodNormalizingExpressionVisitor.VerifyReturnType</c>,
///         the first stage of <c>QueryTranslationPreprocessor</c> — which is downstream of
///         ADR-006's capture point, so the client never runs it. The <em>server</em> does, and for
///         a wholly shippable query that is enough: the six Northwind
///         <c>…returning_queryable_throws</c> tests pass because the tree reaches the server whole
///         and EF refuses it there. It is only when the split leaves the projection on the client
///         that nobody asks the question — and then the provider answers a query EF declares
///         invalid, in four different wrong ways depending on the shape.
///     </para>
///     <para>
///         Adopting the refusal rather than the answer is the honest call here, and this provider
///         has a reason EF does not: <b>an <c>IQueryable&lt;T&gt;</c> cannot cross the wire.</b>
///         What comes back for such a projection is a materialized list, so the declared element
///         type is a promise the provider cannot keep — which is exactly what
///         <c>Join_with_result_selector_returning_queryable_throws_validation_error</c> saw as
///         <c>InvalidCastException: List&lt;Level3&gt; to IQueryable&lt;Level3&gt;</c>.
///     </para>
///     <para>
///         The walk is EF's, down to which shapes recurse (<c>New</c> and <c>MemberInit</c>, so a
///         queryable hidden in an anonymous type is found) and which method is inspected
///         (<c>Queryable.Select</c> only).
///     </para>
/// </remarks>
internal static class QueryableProjectionValidator
{
    /// <summary>
    ///     Throws <see cref="InvalidOperationException" /> with EF's own message if any
    ///     <c>Select</c> in <paramref name="query" /> projects a query.
    /// </summary>
    public static void Validate(Expression query)
        => new Walker().Visit(query);

    private sealed class Walker : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.IsGenericMethod
                && node.Method.GetGenericMethodDefinition() == QueryableMethods.Select
                && StripQuotes(node.Arguments[1]) is LambdaExpression selector)
            {
                Verify(selector.Body, selector.Parameters[0]);
            }

            return base.VisitMethodCall(node);
        }

        private static Expression StripQuotes(Expression node)
        {
            while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
            {
                node = quote.Operand;
            }

            return node;
        }
    }

    private static void Verify(Expression expression, ParameterExpression lambdaParameter)
    {
        switch (expression)
        {
            case NewExpression newExpression:
                foreach (Expression argument in newExpression.Arguments)
                {
                    Verify(argument, lambdaParameter);
                }

                break;

            case MemberInitExpression memberInit:
                Verify(memberInit.NewExpression, lambdaParameter);
                foreach (MemberBinding binding in memberInit.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                    {
                        Verify(assignment.Expression, lambdaParameter);
                    }
                }

                break;

            default:
                if (IsQuery(expression.Type))
                {
                    throw new InvalidOperationException(
                        CoreStrings.QueryInvalidMaterializationType(
                            new ExpressionPrinter().PrintExpression(
                                Expression.Lambda(expression, lambdaParameter)),
                            ShortDisplayName(expression.Type)));
                }

                break;
        }
    }

    /// <summary>
    ///     Whether a type is one EF refuses to materialize — <c>IQueryable&lt;T&gt;</c> or
    ///     <c>IOrderedEnumerable&lt;T&gt;</c>, or anything implementing either.
    /// </summary>
    private static bool IsQuery(Type type)
    {
        if (type.IsGenericTypeDefinition)
        {
            return false;
        }

        if (type.IsInterface && Matches(type))
        {
            return true;
        }

        foreach (Type implemented in type.GetInterfaces())
        {
            if (Matches(implemented))
            {
                return true;
            }
        }

        return false;

        static bool Matches(Type candidate)
            => candidate.IsGenericType
                && (candidate.GetGenericTypeDefinition() == typeof(IQueryable<>)
                    || candidate.GetGenericTypeDefinition() == typeof(IOrderedEnumerable<>));
    }

    /// <summary>
    ///     EF's <c>ShortDisplayName</c> — <c>IQueryable&lt;string&gt;</c>, not
    ///     <c>IQueryable`1[[System.String, …]]</c>.
    /// </summary>
    /// <remarks>
    ///     Reproduced rather than referenced because EF has it in <c>SharedTypeExtensions</c>, a
    ///     shared <em>source</em> file with no referenceable API — the same reason
    ///     <c>QuerySplitter.DisplayName</c> exists beside it. The built-in aliases are the part
    ///     that matters: the spec assertion compares against a message naming
    ///     <c>IQueryable&lt;string&gt;</c>, and <c>String</c> would not match it.
    /// </remarks>
    private static string ShortDisplayName(Type type)
    {
        var builder = new StringBuilder();
        Append(builder, type);
        return builder.ToString();

        static void Append(StringBuilder builder, Type type)
        {
            if (BuiltInTypeNames.TryGetValue(type, out string? builtIn))
            {
                builder.Append(builtIn);
                return;
            }

            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                Append(builder, underlying);
                builder.Append('?');
                return;
            }

            if (type.IsArray)
            {
                Append(builder, type.GetElementType()!);
                builder.Append('[').Append(',', type.GetArrayRank() - 1).Append(']');
                return;
            }

            if (!type.IsGenericType)
            {
                builder.Append(type.Name);
                return;
            }

            int genericPartIndex = type.Name.IndexOf('`', StringComparison.Ordinal);
            if (genericPartIndex <= 0)
            {
                builder.Append(type.Name);
                return;
            }

            builder.Append(type.Name, 0, genericPartIndex).Append('<');

            Type[] arguments = type.GetGenericArguments();
            for (int i = 0; i < arguments.Length; i++)
            {
                Append(builder, arguments[i]);
                if (i + 1 == arguments.Length)
                {
                    continue;
                }

                builder.Append(',');
                if (!arguments[i + 1].IsGenericParameter)
                {
                    builder.Append(' ');
                }
            }

            builder.Append('>');
        }
    }

    private static readonly Dictionary<Type, string> BuiltInTypeNames = new()
    {
        { typeof(bool), "bool" },
        { typeof(byte), "byte" },
        { typeof(char), "char" },
        { typeof(decimal), "decimal" },
        { typeof(double), "double" },
        { typeof(float), "float" },
        { typeof(int), "int" },
        { typeof(long), "long" },
        { typeof(object), "object" },
        { typeof(sbyte), "sbyte" },
        { typeof(short), "short" },
        { typeof(string), "string" },
        { typeof(uint), "uint" },
        { typeof(ulong), "ulong" },
        { typeof(ushort), "ushort" },
        { typeof(void), "void" },
    };
}
