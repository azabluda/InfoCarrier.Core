// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Rewrites a join key that is an anonymous type into the same key as a
///     <see cref="System.Tuple" />, so that the join can cross the wire.
/// </summary>
/// <remarks>
///     <para>
///         <b>What it is for.</b> A composite join key is written <c>new { a.X, a.Y }</c>, and that
///         type is generated in the CALLER's assembly: the server does not have it, and
///         <see cref="Expressions.TypeAllowlist" /> refuses to name it — rightly, because the way to
///         make an arbitrary client type exist on the server is to emit one at run time, which is
///         the attack surface <c>docs/security-review.md</c> §2 refuses. The boundary analyzer
///         therefore cut BELOW the join, the server ran the two roots, and the client joined them:
///         the answer was right and both tables crossed the wire, with nothing saying so
///         (found 2026-09-16 by comparing the server's SQL with EF's).
///     </para>
///     <para>
///         <b>Why <c>Tuple</c> and not <c>ValueTuple</c>.</b> Measured, both ways: EF refuses to
///         translate a <c>ValueTuple</c> key — <c>Translation of method 'System.ValueTuple.Create'
///         failed</c>, and a <c>new ValueTuple&lt;…&gt;(…)</c> tree fails the same way, with or
///         without member bindings. What EF translates is a <c>NewExpression</c> that CARRIES ITS
///         MEMBERS, which is what an anonymous type produces; <c>Tuple&lt;…&gt;</c> is a class of
///         exactly that shape, already on the allowlist, and it produces the identical SQL,
///         including the null matching an anonymous key gets:
///         <c>ON (a = b OR (a IS NULL AND b IS NULL)) AND …</c>.
///     </para>
///     <para>
///         <b>What it does not touch.</b> A key of a type the caller declared (a class of their own,
///         with its own <c>Equals</c>) is not data and cannot be rewritten; that join still runs on
///         the client. So does a key of more than seven members, which <c>Tuple</c> cannot express
///         without nesting. Both are rare, and both keep today's behaviour rather than gaining a new
///         one.
///     </para>
///     <para>
///         <b>Where it runs.</b> On the captured tree, before the boundary analysis, so that the
///         analyzer sees a key it can ship. The rewrite is semantics-preserving on either side of
///         the boundary: <c>Tuple</c> equality is structural and compares members with
///         <c>EqualityComparer&lt;T&gt;.Default</c>, exactly as an anonymous type does, so a query
///         that ends up running on the client answers the same.
///     </para>
/// </remarks>
internal sealed class JoinKeyRewriter : ExpressionVisitor
{
    private const int MaximumMembers = 7;

    private static readonly HashSet<string> JoinMethods = ["Join", "GroupJoin", "LeftJoin", "RightJoin"];

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var visited = (MethodCallExpression)base.VisitMethodCall(node);

        if (visited.Method.DeclaringType != typeof(Queryable)
            || !JoinMethods.Contains(visited.Method.Name)
            || visited.Arguments.Count != 5
            || !visited.Method.IsGenericMethod)
        {
            return visited;
        }

        Type[] typeArguments = visited.Method.GetGenericArguments();
        if (typeArguments.Length != 4 || !IsAnonymous(typeArguments[2]))
        {
            return visited;
        }

        if (Unquote(visited.Arguments[2]) is not { } outer
            || Unquote(visited.Arguments[3]) is not { } inner
            || outer.Body is not NewExpression { Members: { } members } outerKey
            || inner.Body is not NewExpression { Members: not null } innerKey
            || members.Count is 0 or > MaximumMembers)
        {
            return visited;
        }

        Type tuple = TupleOf([.. members.Select(MemberType)]);
        ConstructorInfo constructor = tuple.GetConstructors().Single();
        MemberInfo[] tupleMembers = [.. Enumerable.Range(1, members.Count).Select(i => tuple.GetProperty($"Item{i}")!)];

        MethodInfo rewritten = visited.Method.GetGenericMethodDefinition()
            .MakeGenericMethod(typeArguments[0], typeArguments[1], tuple, typeArguments[3]);

        return Expression.Call(
            rewritten,
            visited.Arguments[0],
            visited.Arguments[1],
            Expression.Quote(KeySelector(outer, outerKey, constructor, tupleMembers, tuple)),
            Expression.Quote(KeySelector(inner, innerKey, constructor, tupleMembers, tuple)),
            visited.Arguments[4]);
    }

    /// <summary>The same lambda, building a <see cref="System.Tuple" /> instead of the anonymous type.</summary>
    private static LambdaExpression KeySelector(
        LambdaExpression original,
        NewExpression key,
        ConstructorInfo constructor,
        MemberInfo[] members,
        Type tuple)
        => Expression.Lambda(
            typeof(Func<,>).MakeGenericType(original.Parameters[0].Type, tuple),
            Expression.New(constructor, key.Arguments, members),
            original.Parameters);

    private static Type TupleOf(Type[] memberTypes)
        => memberTypes.Length switch
        {
            1 => typeof(Tuple<>).MakeGenericType(memberTypes),
            2 => typeof(Tuple<,>).MakeGenericType(memberTypes),
            3 => typeof(Tuple<,,>).MakeGenericType(memberTypes),
            4 => typeof(Tuple<,,,>).MakeGenericType(memberTypes),
            5 => typeof(Tuple<,,,,>).MakeGenericType(memberTypes),
            6 => typeof(Tuple<,,,,,>).MakeGenericType(memberTypes),
            _ => typeof(Tuple<,,,,,,>).MakeGenericType(memberTypes),
        };

    private static Type MemberType(MemberInfo member)
        => member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException($"'{member.Name}' is neither a property nor a field."),
        };

    private static LambdaExpression? Unquote(Expression expression)
        => expression switch
        {
            UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
            LambdaExpression lambda => lambda,
            _ => null,
        };

    /// <summary>
    ///     Whether the type is one the compiler generated for a <c>new { … }</c>, which is the only
    ///     kind of key this rewrites.
    /// </summary>
    internal static bool IsAnonymous(Type type)
        => type.IsGenericType
            && type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && type.Name.Contains("AnonymousType", StringComparison.Ordinal);
}
