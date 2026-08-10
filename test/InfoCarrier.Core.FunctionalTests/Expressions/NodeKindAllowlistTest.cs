// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     The node-kind third of ADR-008 constraint 2 (milestone M5): a payload may name only the
///     node kinds this wire defines, and within a node, only the operator kinds that node can
///     legitimately carry.
/// </summary>
/// <remarks>
///     <para>
///         The two halves are closed by different mechanisms, and only one of them is code we
///         wrote:
///     </para>
///     <list type="number">
///         <item>
///             The <b>node kind</b> is closed by construction. It is not a wire field at all —
///             <see cref="ExpressionNode.Kind" /> is <c>[JsonIgnore]</c> and abstract, answered by
///             each record's CLR type. What the wire carries is System.Text.Json's <c>$kind</c>
///             polymorphic discriminator, which selects among the <c>[JsonDerivedType]</c>s
///             registered on <see cref="ExpressionNode" /> and fails deserialization for anything
///             else. <see cref="NodeToExpressionTranslator" />'s trailing
///             <c>_ =&gt; throw new NotSupportedException</c> is therefore unreachable from a
///             payload; it guards a locally-constructed subclass, which is worth keeping and is
///             not the security control.
///         </item>
///         <item>
///             The <b>operator kind</b> was open until this test's fixtures were written.
///             <c>BinaryNode.Operator</c> and friends are free strings, and <c>Enum.TryParse</c>
///             admits every <see cref="ExpressionType" /> name, bare numeric strings, and
///             comma-separated combinations. <c>Assign</c> and <c>Throw</c> both reach
///             <c>Expression.MakeBinary</c>/<c>MakeUnary</c> and build a mutation or a throw into
///             a tree the server is about to compile.
///         </item>
///     </list>
/// </remarks>
public class NodeKindAllowlistTest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static NodeToExpressionTranslator Translator()
        => new(
            new TypeNodeResolver(),
            new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()),
            (stub, type) => throw new NotSupportedException("No query roots here."));

    private static ExpressionNode Constant(int value)
        => new ExpressionToNodeTranslator(
                new TypeNodeMapper(),
                new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()))
            .Translate(Expression.Constant(value));

    // ---- 1. The node kind itself ------------------------------------------------------------

    /// <summary>
    ///     The proof that the <c>$kind</c> discriminator is default-deny: a value outside the
    ///     registered set is refused by the deserializer, so no node is ever constructed.
    /// </summary>
    [Theory]
    [InlineData(99)]
    [InlineData(15)] // one past QueryRootStub, the highest defined kind
    [InlineData(-1)]
    public void Unregistered_kind_discriminator_is_refused(int kind)
    {
        string json = $$"""{"$kind":{{kind}},"type":{"name":"System.Int32"},"primitiveValue":42}""";

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<ExpressionNode>(json, JsonOptions));
    }

    /// <summary>
    ///     A payload cannot supply <see cref="ExpressionNode.Kind" /> — it is <c>[JsonIgnore]</c>,
    ///     so a node's kind is whatever its CLR type says and a mismatched one is simply dropped.
    /// </summary>
    [Fact]
    public void Kind_is_not_a_wire_field()
    {
        Assert.NotNull(typeof(ExpressionNode).GetProperty(nameof(ExpressionNode.Kind))!
            .GetCustomAttribute<JsonIgnoreAttribute>());

        string json = """{"$kind":0,"kind":"Invocation","type":{"name":"System.Int32"},"primitiveValue":42}""";
        ExpressionNode node = JsonSerializer.Deserialize<ExpressionNode>(json, JsonOptions)!;

        Assert.IsType<ConstantNode>(node);
        Assert.Equal(NodeKind.Constant, node.Kind);
    }

    /// <summary>
    ///     Registration and the enum must stay in step in both directions. This is what makes the
    ///     "closed by construction" argument above hold over time: a new
    ///     <see cref="NodeKind" /> member with no <c>[JsonDerivedType]</c> is a kind the wire
    ///     cannot carry, and a derived type with no member is a discriminator with no name.
    /// </summary>
    [Fact]
    public void Every_node_kind_has_exactly_one_registered_derived_type()
    {
        var registered = typeof(ExpressionNode)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(a => (NodeKind)(int)a.TypeDiscriminator!)
            .ToList();

        Assert.Equal(
            Enum.GetValues<NodeKind>().OrderBy(k => k),
            registered.OrderBy(k => k));
        Assert.Equal(registered.Count, registered.Distinct().Count());
    }

    // ---- 2. The operator kind ---------------------------------------------------------------

    /// <summary>
    ///     <c>Assign</c> is a legal <see cref="ExpressionType" /> that
    ///     <see cref="Expression.MakeBinary(ExpressionType, Expression, Expression)" /> accepts.
    ///     No expression-tree lambda can contain one, so no client can have produced it, so the
    ///     wire refuses it.
    /// </summary>
    [Theory]
    [InlineData("Assign")]
    [InlineData("AddAssign")]
    [InlineData("Throw")]
    [InlineData("TypeIs")]      // a real operator, but not one a BinaryNode carries
    [InlineData("999")]         // Enum.TryParse accepts a bare number as an undefined value
    [InlineData("Add, Not")]    // and a comma list, [Flags] or not
    [InlineData("add")]         // ordinal, so case matters
    [InlineData("")]
    public void Binary_operator_outside_the_allowlist_is_refused(string op)
    {
        var node = new BinaryNode
        {
            Operator = op,
            Left = Constant(1),
            Right = Constant(2),
            Type = new TypeNode { Name = typeof(int).FullName! },
        };

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Translator().Translate(node));
        Assert.Contains("operator allowlist", ex.Message);
        Assert.Contains(nameof(BinaryNode), ex.Message);
    }

    [Theory]
    [InlineData("Throw")]
    [InlineData("PreIncrementAssign")]
    [InlineData("Add")]         // a real operator, but not one a UnaryNode carries
    [InlineData("999")]
    public void Unary_operator_outside_the_allowlist_is_refused(string op)
    {
        var node = new UnaryNode
        {
            Operator = op,
            Operand = Constant(1),
            Type = new TypeNode { Name = typeof(int).FullName! },
        };

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Translator().Translate(node));
        Assert.Contains("operator allowlist", ex.Message);
        Assert.Contains(nameof(UnaryNode), ex.Message);
    }

    /// <summary>
    ///     A <see cref="TypeBinaryNode" /> reads "TypeEqual, else TypeIs", so before the allowlist
    ///     every other name in the enum silently became a type test.
    /// </summary>
    [Fact]
    public void Type_binary_operator_outside_the_two_is_refused()
    {
        var node = new TypeBinaryNode
        {
            Operator = "Equal",
            Operand = Constant(1),
            TypeOperand = new TypeNode { Name = typeof(int).FullName! },
            Type = new TypeNode { Name = typeof(bool).FullName! },
        };

        NotSupportedException ex = Assert.Throws<NotSupportedException>(
            () => Translator().Translate(node));
        Assert.Contains("operator allowlist", ex.Message);
    }

    /// <summary>
    ///     The allowlist is not so tight that ordinary operators stopped working — the negative
    ///     cases above prove default-deny, and this proves the door still opens.
    /// </summary>
    [Theory]
    [InlineData("Add")]
    [InlineData("AndAlso")]
    [InlineData("Coalesce")]
    [InlineData("GreaterThanOrEqual")]
    public void Allowed_binary_operators_still_translate(string op)
    {
        var node = new BinaryNode
        {
            Operator = op,
            Left = Constant(1),
            Right = Constant(2),
            Type = new TypeNode { Name = typeof(int).FullName! },
        };

        // Coalesce and AndAlso are not defined over `int`; what matters is that the operator was
        // admitted and the refusal, if any, came from Expression's own factory rather than here.
        try
        {
            Assert.NotNull(Translator().Translate(node));
        }
        catch (InvalidOperationException)
        {
            // Expression.MakeBinary's own operand check — past the allowlist, which is the point.
        }
    }
}
