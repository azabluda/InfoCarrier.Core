// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Text.Json;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     Round-trip tests for the expression serializer (B6): canonical tree shapes must
///     survive Expression → <see cref="ExpressionNode" /> → JSON → <see cref="ExpressionNode" />
///     → Expression and remain structurally equivalent.
/// </summary>
public class ExpressionRoundTripTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static ExpressionNode RoundTripNode(ExpressionNode node)
    {
        string json = JsonSerializer.Serialize(node, JsonOptions);
        return JsonSerializer.Deserialize<ExpressionNode>(json, JsonOptions)!;
    }

    private static ExpressionNode ToNode(Expression expression)
        => new ExpressionToNodeTranslator(new TypeNodeMapper(), new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()))
            .Translate(expression);

    [Fact]
    public void Constant_primitive_round_trips()
    {
        ExpressionNode node = ToNode(Expression.Constant(42));
        var constant = Assert.IsType<ConstantNode>(RoundTripNode(node));
        Assert.Equal(42, ((JsonElement)constant.PrimitiveValue!).GetInt32());
    }

    /// <summary>
    ///     A constant declared as <c>object</c> keeps the type of what it actually holds.
    /// </summary>
    /// <remarks>
    ///     EF builds exactly this for <c>GetDatabaseValues</c> on a non-numeric key —
    ///     <c>Equals(EF.Property&lt;object&gt;(e, "Id"), keyValues[0])</c>, where the index
    ///     expression is typed <c>object</c>. Without the value's own type the boxed
    ///     <see cref="Guid" /> came back a string and the comparison was quietly false, so the
    ///     query matched nothing and 49 tests failed on a <see langword="null" /> result rather
    ///     than on an error.
    /// </remarks>
    [Fact]
    public void Constant_boxed_in_object_keeps_the_value_type()
    {
        var guid = Guid.NewGuid();
        ExpressionNode node = ToNode(Expression.Constant(guid, typeof(object)));

        var constant = Assert.IsType<ConstantNode>(RoundTripNode(node));

        var reverse = new NodeToExpressionTranslator(
            new TypeNodeResolver(),
            new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()),
            (stub, type) => throw new NotSupportedException("No query roots here."));

        var rebuilt = (ConstantExpression)reverse.Translate(constant);

        Assert.Equal(guid, rebuilt.Value);

        // Still declared as `object`: the surrounding tree — `object.Equals(object, object)` —
        // is built against that type and rejects a `Guid`-typed operand.
        Assert.Equal(typeof(object), rebuilt.Type);
    }

    [Fact]
    public void Binary_comparison_round_trips()
    {
        ParameterExpression p = Expression.Parameter(typeof(int), "x");
        Expression<Func<int, bool>> lambda = Expression.Lambda<Func<int, bool>>(
            Expression.GreaterThan(p, Expression.Constant(5)), p);

        ExpressionNode node = RoundTripNode(ToNode(lambda));
        var lambdaNode = Assert.IsType<LambdaNode>(node);
        var binary = Assert.IsType<BinaryNode>(lambdaNode.Body);
        Assert.Equal("GreaterThan", binary.Operator);
        Assert.IsType<ParameterNode>(binary.Left);
    }

    [Fact]
    public void Method_call_round_trips()
    {
        Expression<Func<string, bool>> lambda = s => s.StartsWith("a");
        ExpressionNode node = RoundTripNode(ToNode(lambda));
        var lambdaNode = Assert.IsType<LambdaNode>(node);
        var call = Assert.IsType<MethodCallNode>(lambdaNode.Body);
        Assert.Equal("StartsWith", call.Method.Name);
    }

    [Fact]
    public void Member_access_round_trips()
    {
        Expression<Func<string, int>> lambda = s => s.Length;
        ExpressionNode node = RoundTripNode(ToNode(lambda));
        var lambdaNode = Assert.IsType<LambdaNode>(node);
        var member = Assert.IsType<MemberNode>(lambdaNode.Body);
        Assert.Equal("Length", member.MemberName);
        Assert.Equal(MemberKind.Property, member.MemberKind);
    }

    [Fact]
    public void Full_tree_round_trips_to_equivalent_expression()
    {
        Expression<Func<int, int, int>> lambda = (a, b) => a * b + 1;
        var forward = new ExpressionToNodeTranslator(new TypeNodeMapper(), new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()));

        ExpressionNode node = RoundTripNode(forward.Translate(lambda));

        var reverse = new NodeToExpressionTranslator(
            new TypeNodeResolver(),
            new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()),
            (stub, type) => throw new NotSupportedException("No query roots here."));

        var rebuilt = (Expression<Func<int, int, int>>)reverse.Translate(node);
        Func<int, int, int> compiled = rebuilt.Compile();
        Assert.Equal(lambda.Compile()(3, 4), compiled(3, 4));
    }

    [Fact]
    public void Query_root_stub_round_trips()
    {
        var stub = new QueryRootStubNode
        {
            ElementType = new TypeNode { Name = "System.String", EntityTypeName = "String" },
            Type = new TypeNode { Name = "System.Linq.IQueryable`1" },
        };

        var roundTripped = Assert.IsType<QueryRootStubNode>(RoundTripNode(stub));
        Assert.Equal("System.String", roundTripped.ElementType.Name);
        Assert.Equal("String", roundTripped.ElementType.EntityTypeName);
    }
}
