// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Translates a serializable <see cref="ExpressionNode" /> DTO back into a live
///     <see cref="Expression" /> tree. Direct recursive translation (no visitor hacks).
/// </summary>
/// <remarks>
///     <see cref="QueryRootStubNode" /> rebinds via an injected root factory — the server
///     supplies a factory that produces a real <c>EntityQueryRootExpression</c> /
///     <c>DbSet&lt;T&gt;</c> from its own context and model (research-findings §2). Parameter
///     identity is remapped by name + position (requirements §2.3).
/// </remarks>
public class NodeToExpressionTranslator
{
    private readonly TypeNodeResolver _typeResolver;
    private readonly IDynamicValueMapper _valueMapper;
    private readonly Func<QueryRootStubNode, Type, Expression> _queryRootFactory;
    private readonly Dictionary<string, ParameterExpression> _parameters = new(StringComparer.Ordinal);

    /// <summary>
    ///     Initializes a new instance of the <see cref="NodeToExpressionTranslator" /> class.
    /// </summary>
    /// <param name="typeResolver">Resolves <see cref="TypeNode" /> → CLR type.</param>
    /// <param name="valueMapper">Materializes dynamic constants.</param>
    /// <param name="queryRootFactory">Rebuilds a query root from a stub + element type.</param>
    public NodeToExpressionTranslator(
        TypeNodeResolver typeResolver,
        IDynamicValueMapper valueMapper,
        Func<QueryRootStubNode, Type, Expression> queryRootFactory)
    {
        _typeResolver = typeResolver;
        _valueMapper = valueMapper;
        _queryRootFactory = queryRootFactory;
    }

    /// <summary>
    ///     Translates a node DTO to a live expression.
    /// </summary>
    public Expression Translate(ExpressionNode node)
    {
        _parameters.Clear();
        return TranslateNode(node);
    }

    private Expression TranslateNode(ExpressionNode node)
        => node switch
        {
            ConstantNode n => TranslateConstant(n),
            ParameterNode n => TranslateParameter(n),
            MemberNode n => TranslateMember(n),
            MethodCallNode n => TranslateMethodCall(n),
            LambdaNode n => TranslateLambda(n),
            NewNode n => TranslateNew(n),
            NewArrayNode n => TranslateNewArray(n),
            BinaryNode n => TranslateBinary(n),
            UnaryNode n => TranslateUnary(n),
            TypeBinaryNode n => TranslateTypeBinary(n),
            ConditionalNode n => TranslateConditional(n),
            MemberInitNode n => TranslateMemberInit(n),
            ListInitNode n => TranslateListInit(n),
            InvocationNode n => TranslateInvocation(n),
            QueryRootStubNode n => _queryRootFactory(n, _typeResolver.Resolve(n.ElementType)),
            _ => throw new NotSupportedException($"Unsupported node kind: {node.Kind}."),
        };

    private Expression TranslateConstant(ConstantNode node)
    {
        Type type = _typeResolver.Resolve(node.Type);
        object? value = node.DynamicValue is not null
            ? _valueMapper.FromDynamicValue(node.DynamicValue)
            : node.PrimitiveValue;
        return Expression.Constant(value, type);
    }

    private ParameterExpression TranslateParameter(ParameterNode node)
    {
        string key = node.Name ?? string.Empty;
        if (!_parameters.TryGetValue(key, out ParameterExpression? parameter))
        {
            parameter = Expression.Parameter(_typeResolver.Resolve(node.Type), node.Name);
            _parameters[key] = parameter;
        }

        return parameter;
    }

    private Expression TranslateMember(MemberNode node)
    {
        Type declaringType = _typeResolver.Resolve(node.DeclaringType);
        Expression? instance = node.Instance is null ? null : TranslateNode(node.Instance);
        MemberInfo member = node.MemberKind == MemberKind.Property
            ? (MemberInfo?)declaringType.GetProperty(node.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Property '{node.MemberName}' not found on '{declaringType}'.")
            : declaringType.GetField(node.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                ?? throw new InvalidOperationException($"Field '{node.MemberName}' not found on '{declaringType}'.");
        return Expression.MakeMemberAccess(instance, member);
    }

    private Expression TranslateMethodCall(MethodCallNode node)
    {
        MethodInfo method = ResolveMethod(node.Method);
        Expression? instance = node.Instance is null ? null : TranslateNode(node.Instance);
        Expression[] arguments = node.Arguments.Select(TranslateNode).ToArray();
        return Expression.Call(instance, method, arguments);
    }

    private MethodInfo ResolveMethod(MethodNode node)
    {
        Type declaringType = _typeResolver.Resolve(node.DeclaringType);
        Type[] parameterTypes = node.ParameterTypes.Select(_typeResolver.Resolve).ToArray();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        IEnumerable<MethodInfo> candidates = declaringType
            .GetMethods(flags)
            .Where(m => m.Name == node.Name && m.GetParameters().Length == parameterTypes.Length);

        foreach (MethodInfo candidate in candidates)
        {
            MethodInfo method = candidate.IsGenericMethodDefinition && node.GenericArguments.Count > 0
                ? candidate.MakeGenericMethod(node.GenericArguments.Select(_typeResolver.Resolve).ToArray())
                : candidate;

            if (method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes))
            {
                return method;
            }
        }

        throw new InvalidOperationException($"Method '{node.Name}' not resolvable on '{declaringType}'.");
    }

    private Expression TranslateLambda(LambdaNode node)
    {
        var parameters = node.Parameters
            .Select(p => Expression.Parameter(_typeResolver.Resolve(p.Type), p.Name))
            .ToList();
        foreach (ParameterExpression p in parameters)
        {
            _parameters[p.Name ?? string.Empty] = p;
        }

        Expression body = TranslateNode(node.Body);
        Type delegateType = _typeResolver.Resolve(node.Type);
        return Expression.Lambda(delegateType, body, parameters);
    }

    private Expression TranslateNew(NewNode node)
    {
        Type type = _typeResolver.Resolve(node.Type);
        Expression[] arguments = node.Arguments.Select(TranslateNode).ToArray();
        Type[] parameterTypes = node.ConstructorParameterTypes.Select(_typeResolver.Resolve).ToArray();
        ConstructorInfo? ctor = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: parameterTypes,
            modifiers: null);
        if (ctor is null)
        {
            throw new InvalidOperationException($"Constructor not found on '{type}'.");
        }

        if (node.Members is { Count: > 0 } members)
        {
            MemberInfo[] memberInfos = members
                .Select(m => (MemberInfo?)type.GetProperty(m) ?? type.GetField(m)
                    ?? throw new InvalidOperationException($"Member '{m}' not found on '{type}'."))
                .ToArray();
            return Expression.New(ctor, arguments, memberInfos);
        }

        return Expression.New(ctor, arguments);
    }

    private Expression TranslateNewArray(NewArrayNode node)
    {
        Type arrayType = _typeResolver.Resolve(node.Type);
        Type elementType = arrayType.GetElementType()
            ?? (arrayType.IsGenericType ? arrayType.GetGenericArguments()[0] : typeof(object));
        Expression[] expressions = node.Expressions.Select(TranslateNode).ToArray();
        return Expression.NewArrayInit(elementType, expressions);
    }

    private Expression TranslateBinary(BinaryNode node)
    {
        Expression left = TranslateNode(node.Left);
        Expression right = TranslateNode(node.Right);
        ExpressionType op = ParseOperator(node.Operator);
        MethodInfo? method = node.Method is null ? null : ResolveMethod(node.Method);
        return Expression.MakeBinary(op, left, right, node.IsLiftedToNull, method);
    }

    private Expression TranslateUnary(UnaryNode node)
    {
        Expression operand = TranslateNode(node.Operand);
        ExpressionType op = ParseOperator(node.Operator);
        Type type = _typeResolver.Resolve(node.Type);
        MethodInfo? method = node.Method is null ? null : ResolveMethod(node.Method);
        return Expression.MakeUnary(op, operand, type, method);
    }

    private Expression TranslateTypeBinary(TypeBinaryNode node)
    {
        Expression operand = TranslateNode(node.Operand);
        Type typeOperand = _typeResolver.Resolve(node.TypeOperand);
        return ParseOperator(node.Operator) == ExpressionType.TypeEqual
            ? Expression.TypeEqual(operand, typeOperand)
            : Expression.TypeIs(operand, typeOperand);
    }

    private Expression TranslateConditional(ConditionalNode node)
        => Expression.Condition(
            TranslateNode(node.Test),
            TranslateNode(node.IfTrue),
            TranslateNode(node.IfFalse),
            _typeResolver.Resolve(node.Type));

    private Expression TranslateMemberInit(MemberInitNode node)
    {
        var newExpression = (NewExpression)TranslateNew(node.NewExpression);
        var bindings = node.Bindings
            .Select(b =>
            {
                Type declaringType = _typeResolver.Resolve(b.DeclaringType);
                MemberInfo member = b.MemberKind == MemberKind.Property
                    ? (MemberInfo?)declaringType.GetProperty(b.MemberName)
                        ?? throw new InvalidOperationException($"Property '{b.MemberName}' not found.")
                    : declaringType.GetField(b.MemberName)
                        ?? throw new InvalidOperationException($"Field '{b.MemberName}' not found.");
                return Expression.Bind(member, TranslateNode(b.Value));
            })
            .ToArray();
        return Expression.MemberInit(newExpression, bindings);
    }

    private Expression TranslateListInit(ListInitNode node)
    {
        var newExpression = (NewExpression)TranslateNew(node.NewExpression);
        var initializers = node.Initializers
            .Select(i => Expression.ElementInit(
                ResolveMethod(i.AddMethod),
                i.Arguments.Select(TranslateNode).ToArray()))
            .ToArray();
        return Expression.ListInit(newExpression, initializers);
    }

    private Expression TranslateInvocation(InvocationNode node)
        => Expression.Invoke(
            TranslateNode(node.Expression),
            node.Arguments.Select(TranslateNode).ToArray());

    private static ExpressionType ParseOperator(string name)
        => Enum.TryParse(name, out ExpressionType result)
            ? result
            : throw new NotSupportedException($"Unsupported operator '{name}'.");
}
