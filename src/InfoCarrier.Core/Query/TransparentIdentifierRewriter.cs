// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Replaces a client-only carrier type that the query creates and consumes internally with a
///     <see cref="ValueTuple" />, so the operators above it stay on the server
///     (<c>docs/transparent-identifiers.md</c> §3.2,
///     [ADR-011](../../../docs/decisions.md#adr-011)).
/// </summary>
/// <remarks>
///     <para>
///         `from c in cs from o in c.Orders where … select c` has no anonymous type in it. The C#
///         compiler puts one there — <c>SelectMany(cs, c => c.Orders, (c, o) => new { c, o })</c> —
///         so that the later clauses can still see <c>c</c>. EF handles that natively; this
///         provider cannot, because an anonymous type is by definition absent from the server's
///         assembly, so [ADR-010](../../../docs/decisions.md#adr-010) makes it a type boundary and
///         every operator above it falls to the client. The client then reads a navigation the
///         server never sent, and the split refuses rather than answer <c>0</c>.
///     </para>
///     <para>
///         The condition this pass actually tests is not "is it a transparent identifier" — that
///         is a fact about the C# compiler, not about the tree — but the structural property that
///         matters: <b>the type is created inside the query and never reaches its result</b>. No
///         caller can observe it, so nothing is owed a reassembly and there is nothing to rebuild.
///         That is what separates this from the reassembly deferral that failed: that one had to
///         move a projection the caller had asked for, and prove each move safe.
///     </para>
///     <para>
///         Two things this pass does <em>not</em> decide. Whether the rewritten tree is an
///         improvement is <see cref="RewriteVerifier" />'s question, and this pass is expected to
///         be discarded whenever it is not. And whether the server can translate the result is
///         nobody's question here — server-ok is a type property, not a translatability one
///         (spec §4), which is why the phase is measured against the suite rather than argued for.
///     </para>
/// </remarks>
internal static class TransparentIdentifierRewriter
{
    /// <summary>
    ///     Rewrites the internal carriers of <paramref name="query" />, or returns it unchanged
    ///     when there are none.
    /// </summary>
    internal static Expression Rewrite(Expression query, TypeAllowlist allowlist)
    {
        Dictionary<Type, MemberInfo[]> carriers = CarrierFinder.Find(query, allowlist);
        if (carriers.Count == 0)
        {
            return query;
        }

        try
        {
            return new Rewriter(carriers).Visit(query);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            // A node kind this pass does not know how to retype. Rebuilding one with mismatched
            // types is exactly what the expression API refuses, so the refusal arrives here
            // rather than as a wrong answer — and the original tree is a perfectly good answer.
            // Deliberately not a silent success: the caller still verifies before keeping
            // anything, so a rewrite that cannot be built and one that does not help take the
            // same path.
            return query;
        }
    }

    private static bool IsSequence(Type type)
        => type != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(type);

    private static Type MemberType(MemberInfo member)
        => member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new InvalidOperationException($"'{member.Name}' is neither a field nor a property."),
        };

    /// <summary>
    ///     Every type occurring in <paramref name="type" />, including through generic arguments.
    /// </summary>
    private static void CollectTypes(Type type, HashSet<Type> into)
    {
        if (!into.Add(type))
        {
            return;
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                CollectTypes(argument, into);
            }
        }

        if (type.GetElementType() is { } element)
        {
            CollectTypes(element, into);
        }
    }

    /// <summary>
    ///     Finds the carrier types worth replacing.
    /// </summary>
    private sealed class CarrierFinder(TypeAllowlist allowlist) : ExpressionVisitor
    {
        private readonly Dictionary<Type, MemberInfo[]> _candidates = [];
        private readonly HashSet<Type> _disqualified = [];

        public static Dictionary<Type, MemberInfo[]> Find(Expression query, TypeAllowlist allowlist)
        {
            var finder = new CarrierFinder(allowlist);
            finder.Visit(query);

            // A type the query returns is the caller's, not the query's. Rewriting one would
            // change what the caller receives — and the split has a mechanism for those already:
            // `ProjectionRewriter` rewrites the projection and rebuilds the type on the client.
            var reachable = new HashSet<Type>();
            CollectTypes(query.Type, reachable);

            foreach (Type type in reachable.Concat(finder._disqualified))
            {
                finder._candidates.Remove(type);
            }

            return finder._candidates;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            // A value of the carrier type already exists, so the type cannot be replaced: there
            // is no rewrite of a constant that turns one into a tuple.
            Disqualify(node.Type);
            return node;
        }

        /// <summary>
        ///     A widening conversion hides the carrier from the query's signature while the value
        ///     still reaches the caller.
        /// </summary>
        /// <remarks>
        ///     Measured: <c>select new { c, o }</c> followed by <c>.Cast&lt;object&gt;()</c> makes
        ///     the query an <c>IQueryable&lt;object&gt;</c>, so the "does the carrier reach the
        ///     result type" test says no — and the caller receives a boxed tuple where it asked
        ///     for the anonymous type. Checking the declared result type is therefore not enough;
        ///     the conversion out of the carrier has to be caught where it happens.
        /// </remarks>
        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
                    or ExpressionType.TypeAs
                && node.Type != node.Operand.Type)
            {
                Disqualify(node.Operand.Type);
            }

            return base.VisitUnary(node);
        }

        private void Disqualify(Type type)
        {
            var types = new HashSet<Type>();
            CollectTypes(type, types);
            _disqualified.UnionWith(types);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // The same escape as a conversion, spelled as an operator.
            if (node.Method.DeclaringType is { } declaring
                && (declaring == typeof(Queryable) || declaring == typeof(Enumerable))
                && node.Method.Name is nameof(Queryable.Cast) or nameof(Queryable.OfType)
                && node.Arguments.Count > 0)
            {
                Disqualify(ServerBoundaryAnalyzer.SequenceElementType(node.Arguments[0].Type));
            }

            if (ProjectionRewriter.IsResultSelectorOperator(node, out LambdaExpression? selector)
                && selector!.Body is NewExpression { Members: { } members } construction
                && members.Count > 0
                && !allowlist.IsAllowed(construction.Type))
            {
                if (construction.Arguments.Any(a => IsSequence(a.Type)))
                {
                    // The guard the reassembly deferral violated. A slot holding a sequence asks
                    // SQL to navigate out of a projected tuple back into a correlated collection
                    // -- `t.Item2.DefaultIfEmpty()` -- and 107 translation failures followed
                    // (spec §4).
                    _disqualified.Add(construction.Type);
                }
                else
                {
                    _candidates[construction.Type] = [.. members];
                }
            }

            return base.VisitMethodCall(node);
        }
    }

    /// <summary>
    ///     Retypes the tree: carrier constructions become tuple constructions, member reads
    ///     through one become slot reads, and every type mentioning a carrier is mapped through.
    /// </summary>
    private sealed class Rewriter(IReadOnlyDictionary<Type, MemberInfo[]> carriers) : ExpressionVisitor
    {
        private readonly Dictionary<Type, Type> _mapped = [];
        private readonly Dictionary<ParameterExpression, ParameterExpression> _parameters
            = new(ReferenceEqualityComparer.Instance);

        private Type Map(Type type)
        {
            if (_mapped.TryGetValue(type, out Type? cached))
            {
                return cached;
            }

            Type result = Compute(type);
            _mapped[type] = result;
            return result;
        }

        private Type Compute(Type type)
        {
            if (carriers.TryGetValue(type, out MemberInfo[]? members))
            {
                return TupleCarrier.MakeType([.. members.Select(m => Map(MemberType(m)))]);
            }

            if (type.IsGenericType)
            {
                Type[] original = type.GetGenericArguments();
                Type[] arguments = [.. original.Select(Map)];
                if (!arguments.SequenceEqual(original))
                {
                    return type.GetGenericTypeDefinition().MakeGenericType(arguments);
                }
            }

            return type;
        }

        protected override Expression VisitNew(NewExpression node)
            => carriers.ContainsKey(node.Type)
                ? TupleCarrier.New([.. node.Arguments.Select(a => Visit(a))])
                : base.VisitNew(node);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is { } inner
                && carriers.TryGetValue(inner.Type, out MemberInfo[]? members)
                && Array.FindIndex(members, m => m.Name == node.Member.Name) is >= 0 and int slot)
            {
                return TupleCarrier.Read(Visit(inner), slot);
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_parameters.TryGetValue(node, out ParameterExpression? replacement))
            {
                return replacement;
            }

            Type mapped = Map(node.Type);
            if (mapped == node.Type)
            {
                return node;
            }

            replacement = Expression.Parameter(mapped, node.Name);
            _parameters[node] = replacement;
            return replacement;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // The parameters have to be rewritten before the body, or the body's references to
            // them would each mint a different replacement.
            ParameterExpression[] parameters = [.. node.Parameters.Select(p => (ParameterExpression)VisitParameter(p))];
            Expression body = Visit(node.Body);

            if (ReferenceEquals(body, node.Body) && parameters.SequenceEqual(node.Parameters))
            {
                return node;
            }

            // Map the delegate type rather than letting `Expression.Lambda` re-infer it from the
            // body. `SelectMany`'s collection selector is declared
            // `Func<TSource, IEnumerable<TCollection>>` while its body — a collection navigation —
            // is an `ICollection<TCollection>`; inference produces the narrower delegate, the
            // rebuilt call no longer matches the operator's parameter, and the whole rewrite is
            // discarded by the catch in `Rewrite`. That is what kept the family this phase exists
            // for from moving at all.
            return Expression.Lambda(Map(node.Type), body, node.Name, node.TailCall, parameters);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Expression operand = Visit(node.Operand);
            Type type = Map(node.Type);

            if (ReferenceEquals(operand, node.Operand) && type == node.Type)
            {
                return node;
            }

            // `Update` keeps the original type, which is the whole thing that changed. A quoted
            // lambda is the common case: its type is `Expression<Func<carrier, …>>`.
            return node.NodeType == ExpressionType.Quote
                ? Expression.Quote(operand)
                : Expression.MakeUnary(node.NodeType, operand, type, node.Method);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression? instance = Visit(node.Object);
            Expression[] arguments = [.. node.Arguments.Select(a => Visit(a))];

            MethodInfo method = node.Method;
            if (method.IsGenericMethod)
            {
                Type[] original = method.GetGenericArguments();
                Type[] generics = [.. original.Select(Map)];
                if (!generics.SequenceEqual(original))
                {
                    method = method.GetGenericMethodDefinition().MakeGenericMethod(generics);
                }
            }

            return ReferenceEquals(method, node.Method)
                && ReferenceEquals(instance, node.Object)
                && arguments.SequenceEqual(node.Arguments)
                    ? node
                    : Expression.Call(instance, method, arguments);
        }
    }
}
