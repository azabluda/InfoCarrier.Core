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
///         (<c>Queryable.Select</c> only). <b>Beside it, and not part of it</b>, is
///         <see cref="VerifyResultType" /> — the same refusal stated on the query's own result
///         element type, which is what catches a queryable produced by a result selector rather
///         than by a <c>Select</c> (C73).
///     </para>
/// </remarks>
internal static class QueryableProjectionValidator
{
    /// <summary>
    ///     Throws <see cref="InvalidOperationException" /> with EF's own message if any
    ///     <c>Select</c> in <paramref name="query" /> projects a query, or if the query's own
    ///     result element type is one.
    /// </summary>
    public static void Validate(Expression query)
    {
        new Walker().Visit(query);
        VerifyResultType(query);
    }

    /// <summary>
    ///     Refuses a query whose <em>result element type</em> is itself a query, whichever
    ///     operator produced it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         EF's walk above inspects <c>Queryable.Select</c> and nothing else, so a queryable
    ///         that comes out of a <b>result selector</b> — a <c>Join</c>'s, a <c>GroupJoin</c>'s,
    ///         a <c>SelectMany</c>'s — is missed. EF knows: its own
    ///         <c>Join_with_result_selector_returning_queryable_throws_validation_error</c> is
    ///         named for the error it does not raise, and every provider overrides it to assert
    ///         whatever its own pipeline happens to fail with instead — <c>ArgumentException</c>
    ///         from InMemory's shaper, <c>ApplyNotSupported</c> from SQLite's translator. The test
    ///         name is the specification; the four overrides are the symptom.
    ///     </para>
    ///     <para>
    ///         This check is stated on the result element type rather than on more operators
    ///         because that is the sentence EF's own message makes — <em>collections in the
    ///         <b>final</b> projection must be an <c>IEnumerable&lt;T&gt;</c></em> — and because
    ///         widening the walk would also refuse an <c>IOrderedEnumerable&lt;T&gt;</c> sitting in
    ///         an intermediate transparent identifier, which EF permits and which a later
    ///         <c>Select</c> is free to consume.
    ///     </para>
    ///     <para>
    ///         And this provider has a reason EF does not, C56's: <b>an
    ///         <c>IQueryable&lt;T&gt;</c> cannot cross the wire.</b> What comes back is a
    ///         materialized list, so the declared element type is a promise the provider cannot
    ///         keep — which is what the four saw as
    ///         <c>InvalidCastException: List&lt;Level3&gt; to IQueryable&lt;Level3&gt;</c>, raised
    ///         by a reflective <c>Invoke</c> in <c>QueryExecutor</c> rather than by anything that
    ///         means to say it.
    ///     </para>
    /// </remarks>
    private static void VerifyResultType(Expression query)
    {
        if (SequenceElementType(query.Type) is not { } element || !IsQuery(element))
        {
            return;
        }

        throw new InvalidOperationException(
            CoreStrings.QueryInvalidMaterializationType(Describe(query, element), ShortDisplayName(element)));
    }

    /// <summary>
    ///     The offending projection, quoted back the way EF quotes one: the result selector that
    ///     produced the element type where the root operator has one, and the query otherwise.
    /// </summary>
    private static string Describe(Expression query, Type element)
    {
        if (query is MethodCallExpression { Arguments.Count: > 1 } call)
        {
            for (int i = call.Arguments.Count - 1; i >= 1; i--)
            {
                if (StripQuotes(call.Arguments[i]) is LambdaExpression { ReturnType: { } returnType } selector
                    && returnType == element)
                {
                    return new ExpressionPrinter().PrintExpression(selector);
                }
            }
        }

        return new ExpressionPrinter().PrintExpression(query);
    }

    /// <summary>
    ///     The <c>T</c> of a sequence type, or <see langword="null" /> if it is not one.
    /// </summary>
    private static Type? SequenceElementType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type.GetGenericArguments()[0];
        }

        Type? found = null;
        foreach (Type implemented in type.GetInterfaces())
        {
            if (implemented.IsGenericType && implemented.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                // More than one `IEnumerable<>` means the type is not a sequence *of* anything in
                // particular, and guessing which one is the element would be worse than declining.
                if (found is not null)
                {
                    return null;
                }

                found = implemented.GetGenericArguments()[0];
            }
        }

        return found;
    }

    private static Expression StripQuotes(Expression node)
    {
        while (node is UnaryExpression { NodeType: ExpressionType.Quote } quote)
        {
            node = quote.Operand;
        }

        return node;
    }

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
