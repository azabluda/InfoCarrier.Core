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
    private readonly IDynamicValueMapper _valueMapper;

    // Parameter identity for the message being translated (requirements §2.3). Expression does
    // not override Equals/GetHashCode, so this dictionary already keys on reference identity —
    // exactly what "the same parameter" means here.
    private readonly Dictionary<ParameterExpression, int> _parameterIds = [];
    private int _depth;
    private ExpressionNode? _result;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ExpressionToNodeTranslator" /> class.
    /// </summary>
    public ExpressionToNodeTranslator(TypeNodeMapper typeMapper, IDynamicValueMapper valueMapper)
    {
        _typeMapper = typeMapper;
        _valueMapper = valueMapper;
    }

    /// <summary>
    ///     Translates an expression to its node DTO.
    /// </summary>
    public ExpressionNode Translate(Expression expression)
    {
        // Translate is both the public entry point and the recursive one, so parameter identity
        // is reset only at depth 0 — clearing on every call would break identity mid-tree.
        if (_depth == 0)
        {
            _parameterIds.Clear();
        }

        _depth++;
        try
        {
            _result = null;
            Visit(expression);
            return _result ?? throw new InvalidOperationException("Translation produced no node.");
        }
        finally
        {
            _depth--;
        }
    }

    /// <summary>
    ///     Maps a parameter to its node, assigning a message-stable id on first sight.
    /// </summary>
    private ParameterNode ToParameterNode(ParameterExpression parameter)
    {
        if (!_parameterIds.TryGetValue(parameter, out int id))
        {
            id = _parameterIds.Count;
            _parameterIds[parameter] = id;
        }

        return new ParameterNode
        {
            Id = id,
            Name = parameter.Name,
            Type = _typeMapper.ToTypeNode(parameter.Type),
        };
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

        // EF queryable roots (DbSet<T> / EntityQueryRootExpression-backed IQueryables) become
        // query-root stubs (research-findings §2).
        if (node.Value is IQueryable queryable)
        {
            _result = new QueryRootStubNode
            {
                ElementType = RootElementType(
                    queryable.ElementType,
                    queryable.Expression as Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression),
                Type = type,
            };
            return node;
        }

        _result = IsPrimitive(node.Value)
            ? new ConstantNode
            {
                Type = type,
                ValueType = node.Value is not null && node.Value.GetType() != node.Type
                    ? _typeMapper.ToTypeNode(node.Value.GetType())
                    : null,
                PrimitiveValue = NormalizePrimitive(node.Value),
            }
            // Mapped by what the value *is*, not by what the expression declares it as. The two
            // differ whenever a comparison is made through `object`, which is exactly what
            // `EntityFinder` builds for `Find`: `Equals(EF.Property(e, "Id"), <object constant>)`.
            // Mapping a `Microsoft.EntityFrameworkCore.…IntStructKey` as `object` walked `object`'s
            // members — there are none — so the server rebuilt a bare `new object()` and every
            // `Find` on a value-converted key matched nothing.
            //
            // Only the *node* widens; `ConstantNode.Type` still carries the declared type, so the
            // rebuilt `Expression.Constant` keeps the type its parent expects. The truncation this
            // used to give is deliberate one level down, in `MapToNode`'s member walk, where a
            // member declared `object` holding a `DbContext` must stay truncated — and that is
            // untouched.
            : new ConstantNode
            {
                Type = type,
                DynamicValue = _valueMapper.ToDynamicValue(node.Value, node.Value?.GetType() ?? node.Type),
            };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitExtension(Expression node)
    {
        // EF Core's EntityQueryRootExpression (NodeType Extension) becomes a query-root stub.
        if (node is Microsoft.EntityFrameworkCore.Query.QueryRootExpression root)
        {
            _result = new QueryRootStubNode
            {
                ElementType = RootElementType(
                    root.ElementType,
                    root as Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression),
                Type = _typeMapper.ToTypeNode(node.Type),
            };
            return node;
        }

        throw new NotSupportedException($"Unsupported extension expression: {node.GetType()}.");
    }

    /// <summary>
    ///     The wire identity of a query root's element type, naming the EF entity type when the
    ///     CLR type alone cannot.
    /// </summary>
    /// <remarks>
    ///     <see cref="TypeNodeMapper" /> fills <see cref="TypeNode.EntityTypeName" /> by looking
    ///     the CLR type up in the model, and that lookup cannot answer for a shared-type entity:
    ///     every many-to-many join entity is a <c>Dictionary&lt;string, object&gt;</c> and several
    ///     of them are that same type, told apart only by name. So the name came out null, the
    ///     server fell back to resolving the CLR type, and the root failed to bind —
    ///     "Entity type 'System.Collections.Generic.Dictionary`2&lt;System.String,System.Object&gt;'
    ///     not found in the server model". At a query root the expression itself carries the
    ///     entity type, so nothing has to be inferred.
    /// </remarks>
    private TypeNode RootElementType(
        Type elementType,
        Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression? root)
    {
        TypeNode node = _typeMapper.ToTypeNode(elementType);

        return root is null || node.EntityTypeName is not null
            ? node
            : node with { EntityTypeName = root.EntityType.Name };
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        _result = ToParameterNode(node);
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
            .Select(ToParameterNode)
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

    /// <inheritdoc />
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        ExpressionNode operand = Translate(node.Expression);
        _result = new TypeBinaryNode
        {
            Operator = node.NodeType.ToString(),
            Operand = operand,
            TypeOperand = _typeMapper.ToTypeNode(node.TypeOperand),
            Type = _typeMapper.ToTypeNode(node.Type),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitConditional(ConditionalExpression node)
    {
        ExpressionNode test = Translate(node.Test);
        ExpressionNode ifTrue = Translate(node.IfTrue);
        ExpressionNode ifFalse = Translate(node.IfFalse);
        _result = new ConditionalNode
        {
            Test = test,
            IfTrue = ifTrue,
            IfFalse = ifFalse,
            Type = _typeMapper.ToTypeNode(node.Type),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        var newNode = (NewNode)Translate(node.NewExpression);
        var bindings = node.Bindings
            .Where(b => b.BindingType == MemberBindingType.Assignment)
            .Select(b => new MemberBindingNode
            {
                DeclaringType = _typeMapper.ToTypeNode(b.Member.DeclaringType!),
                MemberName = b.Member.Name,
                MemberKind = b.Member is PropertyInfo ? MemberKind.Property : MemberKind.Field,
                Value = Translate(((MemberAssignment)b).Expression),
            })
            .ToList();
        _result = new MemberInitNode
        {
            NewExpression = newNode,
            Bindings = bindings,
            Type = _typeMapper.ToTypeNode(node.Type),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitListInit(ListInitExpression node)
    {
        var newNode = (NewNode)Translate(node.NewExpression);
        var initializers = node.Initializers
            .Select(i => new ElementInitNode
            {
                AddMethod = ToMethodNode(i.AddMethod),
                Arguments = i.Arguments.Select(Translate).ToList(),
            })
            .ToList();
        _result = new ListInitNode
        {
            NewExpression = newNode,
            Initializers = initializers,
            Type = _typeMapper.ToTypeNode(node.Type),
        };
        return node;
    }

    /// <inheritdoc />
    protected override Expression VisitInvocation(InvocationExpression node)
    {
        ExpressionNode expression = Translate(node.Expression);
        var arguments = node.Arguments.Select(Translate).ToList();
        _result = new InvocationNode
        {
            Expression = expression,
            Arguments = arguments,
            Type = _typeMapper.ToTypeNode(node.Type),
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

    private static object? NormalizePrimitive(object? value)
        => PrimitiveCoercion.Normalize(value);
}
