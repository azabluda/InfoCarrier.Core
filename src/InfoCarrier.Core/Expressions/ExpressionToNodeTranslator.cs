// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Translates a live <see cref="Expression" /> tree into the serializable
///     <see cref="ExpressionNode" /> DTO model. Direct recursive translation via
///     <see cref="ExpressionVisitor" /> — no rlinq <c>ResultWrapperExpression</c> hack.
/// </summary>
/// <remarks>
///     Leaf + composite nodes (A2/A3) are handled here. Init/conditional, query-root stubs,
///     and dynamic constants land in B3; until then those node kinds raise
///     <see cref="NotSupportedException" />.
/// </remarks>
public class ExpressionToNodeTranslator : ExpressionVisitor
{
    private readonly TypeNodeMapper _typeMapper;
    private ExpressionNode? _result;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionToNodeTranslator" /> class.
    /// </summary>
    public ExpressionToNodeTranslator(TypeNodeMapper typeMapper)
        => _typeMapper = typeMapper;

    /// <summary>
    ///     Translates an expression to its node DTO.
    /// </summary>
    public ExpressionNode Translate(Expression expression)
    {
        _result = null;
        Visit(expression);
        return _result ?? throw new InvalidOperationException("Translation produced no node.");
    }

    /// <inheritdoc />
    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            _result = null;
            return null;
        }

        Expression? visited = base.Visit(node);
        return visited;
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
    {
        TypeNode type = _typeMapper.ToTypeNode(node.Type);
        _result = IsPrimitive(node.Value)
            ? new ConstantNode { Type = type, PrimitiveValue = node.Value }
            : new ConstantNode { Type = type, DynamicValue = TranslateDynamicValue(node.Value, node.Type) };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        _result = new ParameterNode { Name = node.Name, Type = _typeMapper.ToTypeNode(node.Type) };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        ExpressionNode? instance = node.Expression is null ? null : Translate(node.Expression);
        _result = new MemberNode
        {
            DeclaringType = _typeMapper.ToTypeNode(node.Member.DeclaringType!),
            MemberName = node.Member.Name,
            MemberKind = node.Member is PropertyInfo ? MemberKind.Property : MemberKind.Field,
            Type = _typeMapper.ToTypeNode(node.Type),
            Instance = instance,
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        ExpressionNode? instance = node.Object is null ? null : Translate(node.Object);
        var arguments = node.Arguments.Select(Translate).ToList();
        _result = new MethodCallNode
        {
            Method = ToMethodNode(node.Method),
            Instance = instance,
            Arguments = arguments,
            Type = _typeMapper.ToTypeNode(node.Type),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        var parameters = node.Parameters
            .Select(p => new ParameterNode { Name = p.Name, Type = _typeMapper.ToTypeNode(p.Type) })
            .ToList();
        ExpressionNode body = Translate(node.Body);
        _result = new LambdaNode
        {
            Body = body,
            Parameters = parameters,
            Type = _typeMapper.ToTypeNode(node.Type),
            ReturnType = _typeMapper.ToTypeNode(node.ReturnType),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitNew(NewExpression node)
    {
        var arguments = node.Arguments.Select(Translate).ToList();
        _result = new NewNode
        {
            Type = _typeMapper.ToTypeNode(node.Type),
            ConstructorParameterTypes = node.Constructor is null
                ? []
                : node.Constructor.GetParameters().Select(p => _typeMapper.ToTypeNode(p.ParameterType)).ToList(),
            Arguments = arguments,
            Members = node.Members?.Select(m => m.Name).ToList(),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        var expressions = node.Expressions.Select(Translate).ToList();
        _result = new NewArrayNode
        {
            Type = _typeMapper.ToTypeNode(node.Type),
            Expressions = expressions,
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        ExpressionNode left = Translate(node.Left);
        ExpressionNode right = Translate(node.Right);
        _result = new BinaryNode
        {
            Operator = node.NodeType.ToString(),
            Left = left,
            Right = right,
            Type = _typeMapper.ToTypeNode(node.Type),
            Method = node.Method is null ? null : ToMethodNode(node.Method),
            IsLiftedToNull = node.IsLiftedToNull,
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        ExpressionNode operand = Translate(node.Operand);
        _result = new UnaryNode
        {
            Operator = node.NodeType.ToString(),
            Operand = operand,
            Type = _typeMapper.ToTypeNode(node.Type),
            Method = node.Method is null ? null : ToMethodNode(node.Method),
        };
        return node;
    }

    /// <summary>
    ///     Maps a <see cref="MethodInfo" /> to a re-resolvable <see cref="MethodNode" />.
    /// </summary>
    protected MethodNode ToMethodNode(MethodInfo method)
        => new()
        {
            DeclaringType = _typeMapper.ToTypeNode(method.DeclaringType!),
            Name = method.Name,
            GenericArguments = method.IsGenericMethod
                ? method.GetGenericArguments().Select(_typeMapper.ToTypeNode).ToList()
                : [],
            ParameterTypes = method.GetParameters().Select(p => _typeMapper.ToTypeNode(p.ParameterType)).ToList(),
            ReturnType = _typeMapper.ToTypeNode(method.ReturnType),
        };

    private static bool IsPrimitive(object? value)
        => value is null
            || value.GetType().IsPrimitive
            || value is string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or DateOnly or TimeOnly
            || value.GetType().IsEnum;

    /// <summary>
    ///     Translates a non-primitive constant to a <see cref="DynamicValueNode" />. Implemented
    ///     in B3 (dynamic value mapping); throws until then.
    /// </summary>
    protected virtual DynamicValueNode TranslateDynamicValue(object? value, Type type)
        => throw new NotSupportedException(
            $"Dynamic constant values are handled in B3. Got: {type}.");
}
