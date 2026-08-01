// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using InfoCarrier.Core.Query;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.ProjectionSplit;

/// <summary>
///     Guards <see cref="WireTypeCollector" /> against drifting from
///     <see cref="ExpressionToNodeTranslator" /> (<c>docs/projection-split.md</c> §3.1).
/// </summary>
/// <remarks>
///     The failure this prevents is asymmetric. A type the collector reports but the translator
///     never writes only splits the query earlier than necessary — slower, still correct. A type
///     the translator writes but the collector misses is a type the client ships and the server
///     refuses, at runtime, on a shape no unit test covers. So the assertion is one-directional:
///     everything the translator emits must be visible to the collector.
/// </remarks>
public class WireTypeCollectorTest
{
    private sealed class Dto
    {
        public string? Name { get; set; }

        public int Count;
    }

    // The shapes below exist to make three collector lines observable. Each puts a type on the
    // wire that appears *nowhere else* in its node — remove the corresponding line and the type
    // silently vanishes from the payload.
    private sealed class Ingredient;

    private sealed class Mixed(Ingredient ingredient)
    {
        public Ingredient Ingredient { get; } = ingredient;
    }

    private class Base
    {
        public string? Inherited { get; set; }
    }

    private sealed class Derived : Base;

    private delegate int Measure(string value);

    public static TheoryData<string, Expression> Shapes()
    {
        ParameterExpression s = Expression.Parameter(typeof(string), "s");
        Expression<Func<int, int, int>> arithmetic = (a, b) => a * b + 1;
        Expression<Func<string, bool>> methodCall = x => x.StartsWith("a");
        Expression<Func<string, int>> member = x => x.Length;
        Expression<Func<string, object>> anonymous = x => new { x.Length };
        Expression<Func<string, Dto>> memberInit = x => new Dto { Name = x, Count = 1 };
        Expression<Func<string, List<string>>> listInit = x => new List<string> { x };
        Expression<Func<object, bool>> typeBinary = x => x is string;
        Expression<Func<int, string>> conditional = x => x > 5 ? "a" : "b";
        Expression<Func<int[]>> newArray = () => new[] { 1, 2, 3 };
        Expression<Func<string, int>> nullableAndConvert = x => (int)(long)x.Length;
        Expression<Func<Dto, string?>> fieldAndProperty = d => d.Count > 0 ? d.Name : null;

        // A projection into a client-only type over a queryable chain — the shape this whole
        // milestone exists for.
        IQueryable<string> source = new[] { "a", "bb" }.AsQueryable();
        Expression clientProjection = source
            .Where(x => x.Length > 1)
            .Select(x => new { x, Upper = x.ToUpperInvariant() })
            .Expression;

        Expression invocation = Expression.Invoke(methodCall, s);

        // `Ingredient` is reachable only through the constructor's parameter list; `Base` only
        // through the binding's declaring type; `int` only through the delegate's return type,
        // since `Measure` is not generic and so carries no arguments to expand.
        Expression<Func<Mixed>> ctorParameterOnly = () => new Mixed(new Ingredient());
        Expression<Func<string, Derived>> inheritedBinding = x => new Derived { Inherited = x };
        Expression<Measure> customDelegate = Expression.Lambda<Measure>(
            Expression.Property(s, nameof(string.Length)), s);

        return new TheoryData<string, Expression>
        {
            { "ctorParameterOnly", ctorParameterOnly },
            { "inheritedBinding", inheritedBinding },
            { "customDelegate", customDelegate },
            { "arithmetic", arithmetic },
            { "methodCall", methodCall },
            { "member", member },
            { "anonymous", anonymous },
            { "memberInit", memberInit },
            { "listInit", listInit },
            { "typeBinary", typeBinary },
            { "conditional", conditional },
            { "newArray", newArray },
            { "nullableAndConvert", nullableAndConvert },
            { "fieldAndProperty", fieldAndProperty },
            { "clientProjection", clientProjection },
            { "invocation", Expression.Lambda(invocation, s) },
        };
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Collector_sees_every_type_the_translator_writes(string name, Expression expression)
    {
        var mapper = new TypeNodeMapper();
        var translator = new ExpressionToNodeTranslator(
            mapper, new DynamicValueMapper(null, mapper, new TypeNodeResolver()));

        // Node by node, not whole-tree. A whole-tree comparison is nearly blind: types repeat
        // across a tree, so dropping `MemberExpression.Member.DeclaringType` still passed —
        // `string` was already present as some parameter's type. Comparing each node against its
        // own DTO is what makes an omission visible.
        foreach (Expression current in EnumerateNodes(expression))
        {
            HashSet<string> emitted = FindOwnTypeNodes(translator.Translate(current))
                .Select(t => t.ToString())
                .ToHashSet(StringComparer.Ordinal);

            var collected = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type type in WireTypeCollector.CollectOwn(current))
            {
                AddWithGenericArguments(mapper.ToTypeNode(type), collected);
            }

            string[] missed = emitted.Except(collected).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.True(
                missed.Length == 0,
                $"'{name}', node {current.NodeType} ({current}): the translator writes types the "
                    + $"collector does not report: {string.Join(", ", missed)}");
        }
    }

    [Fact]
    public void Own_types_exclude_children()
    {
        Expression<Func<string, int>> lambda = x => x.Length;
        var body = (MemberExpression)lambda.Body;

        IReadOnlyList<Type> own = WireTypeCollector.CollectOwn(body);

        Assert.Contains(typeof(int), own);        // node.Type
        Assert.Contains(typeof(string), own);     // Member.DeclaringType
        Assert.DoesNotContain(typeof(Func<string, int>), own);  // the lambda is not a child of its body
    }

    [Fact]
    public void A_boxed_constant_reports_its_runtime_type()
    {
        // The declared type is `object`; the dynamic-value graph writes `Dto`. Reporting only
        // the declared type would let a client-only type through unseen.
        ConstantExpression constant = Expression.Constant(new Dto(), typeof(object));

        Assert.Contains(typeof(Dto), WireTypeCollector.CollectOwn(constant));
    }

    private static IEnumerable<Expression> EnumerateNodes(Expression expression)
    {
        var nodes = new List<Expression>();
        new EnumeratingVisitor(nodes).Visit(expression);
        return nodes;
    }

    private sealed class EnumeratingVisitor(ICollection<Expression> nodes) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node)
        {
            if (node is not null)
            {
                nodes.Add(node);
            }

            return base.Visit(node);
        }
    }

    private static void AddWithGenericArguments(TypeNode node, ISet<string> into)
    {
        into.Add(node.ToString());
        foreach (TypeNode argument in node.GenericArguments)
        {
            AddWithGenericArguments(argument, into);
        }
    }

    /// <summary>
    ///     Every <see cref="TypeNode" /> a DTO node writes for <em>itself</em> — found
    ///     reflectively, so a new node kind cannot quietly escape the comparison, and stopping at
    ///     nested <see cref="ExpressionNode" />s, which are the children's own contribution.
    /// </summary>
    private static List<TypeNode> FindOwnTypeNodes(ExpressionNode graph)
    {
        var found = new List<TypeNode>();
        Walk(graph, found, new HashSet<object>(ReferenceEqualityComparer.Instance), isRoot: true);
        return found;
    }

    private static void Walk(object? value, List<TypeNode> found, HashSet<object> seen, bool isRoot = false)
    {
        if (value is ExpressionNode && !isRoot)
        {
            return;
        }

        switch (value)
        {
            case null or string:
                return;
            case TypeNode typeNode:
                found.Add(typeNode);
                foreach (TypeNode argument in typeNode.GenericArguments)
                {
                    Walk(argument, found, seen);
                }

                return;
        }

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || !seen.Add(value))
        {
            return;
        }

        if (value is IEnumerable sequence)
        {
            foreach (object? item in sequence)
            {
                Walk(item, found, seen);
            }

            return;
        }

        // Only our own DTOs are worth descending into; anything else is a leaf value.
        if (type.Namespace?.StartsWith("InfoCarrier.Core", StringComparison.Ordinal) != true)
        {
            return;
        }

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0)
            {
                Walk(property.GetValue(value), found, seen);
            }
        }
    }
}
