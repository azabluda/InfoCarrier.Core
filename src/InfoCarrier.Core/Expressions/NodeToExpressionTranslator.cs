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
/// <remarks>
///     Initializes a new instance of the <see cref="NodeToExpressionTranslator" /> class.
/// </remarks>
/// <param name="typeResolver">Resolves <see cref="TypeNode" /> → CLR type.</param>
/// <param name="valueMapper">Materializes dynamic constants.</param>
/// <param name="queryRootFactory">Rebuilds a query root from a stub + element type.</param>
public class NodeToExpressionTranslator(
    TypeNodeResolver typeResolver,
    IDynamicValueMapper valueMapper,
    Func<QueryRootStubNode, Type, Expression> queryRootFactory)
{
    private readonly TypeNodeResolver _typeResolver = typeResolver;
    private readonly IDynamicValueMapper _valueMapper = valueMapper;
    private readonly Func<QueryRootStubNode, Type, Expression> _queryRootFactory = queryRootFactory;

    /// <summary>
    ///     Supplies the context that fills a <see cref="ServerContextStubNode" />, or
    ///     <see langword="null" /> when nothing has offered one.
    /// </summary>
    /// <remarks>
    ///     <b>An init property rather than a constructor parameter</b>, because this class's
    ///     constructor shipped in <c>10.0.0</c> and adding a parameter to it would be a binary
    ///     break that package validation reports as <c>CP0002</c>. It is set by
    ///     <c>ExpressionSerializer</c>, which the server tells about its context once per
    ///     execution.
    /// </remarks>
    public virtual Func<Type, Expression>? ServerContextFactory { get; init; }
    // Keyed by ParameterNode.Id, never by name — see ParameterNode.Id for why.
    private readonly Dictionary<int, ParameterExpression> _parameters = [];

    /// <summary>
    ///     Translates a node DTO to a live expression.
    /// </summary>
    public virtual Expression Translate(ExpressionNode node)
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
            ServerContextStubNode n => TranslateServerContext(n),
            _ => throw new NotSupportedException($"Unsupported node kind: {node.Kind}."),
        };

    /// <summary>
    ///     Puts this server's own context where the client's stood.
    /// </summary>
    /// <remarks>
    ///     <b>The declared type is checked rather than trusted.</b> The method being called is
    ///     declared on the client's context type, so the server's context must be assignable to it
    ///     or the rebuilt call cannot bind. Saying so by name is better than failing inside
    ///     reflection, and it is the honest report when two halves were built from different
    ///     context classes.
    /// </remarks>
    private Expression TranslateServerContext(ServerContextStubNode node)
    {
        Type declared = _typeResolver.Resolve(node.Type);

        if (ServerContextFactory is not { } factory)
        {
            throw new InvalidOperationException(
                $"The payload names the context type '{declared}' as the receiver of a mapped "
                + "function, but this side has no context to put there. Only a server executing a "
                + "query can resolve that node.");
        }

        return factory(declared);
    }

    private Expression TranslateConstant(ConstantNode node)
    {
        Type type = _typeResolver.Resolve(node.Type);

        // Coerce to what the value *is*, not to what the constant is declared as: a constant
        // typed `object` tells the primitive form nothing, and a boxed Guid would come back a
        // string. The declared type still governs the constant itself, below.
        Type valueType = node.ValueType is { } specific ? _typeResolver.Resolve(specific) : type;

        object? value = node.DynamicValue is not null
            ? _valueMapper.FromDynamicValue(node.DynamicValue)
            : CoercePrimitive(node.PrimitiveValue, valueType);

        // Some declared types cannot be rebuilt exactly — `IOrderedEnumerable<T>` has no public
        // way in, so the value comes back as a plain list. Widening the constant to what we
        // actually hold keeps the tree buildable; every operator that reads such a constant
        // (`Contains` and friends) takes it as `IEnumerable<T>` anyway.
        if (value is not null && !type.IsInstanceOfType(value))
        {
            type = value.GetType();
        }

        return Expression.Constant(value, type);
    }

    private static object? CoercePrimitive(object? value, Type targetType)
        => PrimitiveCoercion.Coerce(value, targetType);

    private ParameterExpression TranslateParameter(ParameterNode node)
    {
        if (!_parameters.TryGetValue(node.Id, out ParameterExpression? parameter))
        {
            parameter = Expression.Parameter(_typeResolver.Resolve(node.Type), node.Name);
            _parameters[node.Id] = parameter;
        }

        return parameter;
    }

    private Expression TranslateMember(MemberNode node)
    {
        Type declaringType = _typeResolver.Resolve(node.DeclaringType);
        Expression? instance = node.Instance is null ? null : TranslateNode(node.Instance);
        MemberInfo member = node.MemberKind == MemberKind.Property
            ? (MemberInfo?)FindMember(declaringType, t => t.GetProperty(node.MemberName, DeclaredMembers))
                ?? throw new InvalidOperationException($"Property '{node.MemberName}' not found on '{declaringType}'.")
            : FindMember(declaringType, t => t.GetField(node.MemberName, DeclaredMembers))
                ?? throw new InvalidOperationException($"Field '{node.MemberName}' not found on '{declaringType}'.");
        return Expression.MakeMemberAccess(instance, member);
    }

    private const BindingFlags DeclaredMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    /// <summary>
    ///     Finds a member on a type or the first base type that declares it, most-derived first.
    /// </summary>
    /// <remarks>
    ///     One level at a time with <see cref="BindingFlags.DeclaredOnly" />, because the plain
    ///     lookup throws on a <c>new</c>-hidden member rather than preferring the nearer one:
    ///     "ambiguous match found for '… BlogHiding … Posts'". A model whose derived type hides a
    ///     base property is legitimate — <c>FieldMappingTestBase</c>'s `*Hiding` entities are
    ///     exactly that — and the most-derived declaration is the one the client wrote and the
    ///     one the wire named.
    /// </remarks>
    private static MemberInfo? FindMember(Type declaringType, Func<Type, MemberInfo?> lookup)
    {
        for (Type? type = declaringType; type is not null; type = type.BaseType)
        {
            if (lookup(type) is { } member)
            {
                return member;
            }
        }

        return null;
    }

    private Expression TranslateMethodCall(MethodCallNode node)
    {
        MethodInfo method = ResolveMethod(node.Method);
        Expression? instance = node.Instance is null ? null : TranslateNode(node.Instance);
        Expression[] arguments = node.Arguments.Select(TranslateNode).ToArray();
        return Expression.Call(instance, method, arguments);
    }

    /// <summary>
    ///     The non-public methods a payload may name, by declaring type and name (ADR-008
    ///     constraint 2, milestone M5).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         **EF's public query API rewrites itself into non-public marker methods**, and a
    ///         provider that captures the tree after those rewrites — which ADR-006 requires —
    ///         necessarily transports them. So visibility alone cannot be the rule; C25 measured
    ///         that at <b>154 → 697</b>. This is the exception list that makes a public-only rule
    ///         workable, and both entries were produced by that measurement rather than guessed:
    ///     </para>
    ///     <list type="bullet">
    ///         <item><c>NotQuiteInclude</c> — what <c>Include("Orders")</c> becomes. 384 failures without it.</item>
    ///         <item><c>ExecuteUpdate</c> — the <c>IReadOnlyList&lt;ITuple&gt;</c> overload C19 spent a step on. 157 without it.</item>
    ///     </list>
    ///     <para>
    ///         Keyed by name rather than by <see cref="MethodInfo" /> because both are generic
    ///         definitions that arrive already constructed, and because a name is what the wire
    ///         actually carries.
    ///     </para>
    /// </remarks>
    private static readonly HashSet<(string DeclaringType, string Name)> AllowedNonPublicMethods =
    [
        (typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions).FullName!, "NotQuiteInclude"),
        (typeof(Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions).FullName!, "ExecuteUpdate"),
    ];

    private MethodInfo ResolveMethod(MethodNode node)
    {
        Type declaringType = _typeResolver.Resolve(node.DeclaringType);
        Type[] parameterTypes = node.ParameterTypes.Select(_typeResolver.Resolve).ToArray();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        Type[] genericArguments = node.GenericArguments.Select(_typeResolver.Resolve).ToArray();
        Type returnType = _typeResolver.Resolve(node.ReturnType);
        MethodInfo? signatureMatch = null;

        IEnumerable<MethodInfo> candidates = declaringType
            .GetMethods(flags)
            .Where(m => m.Name == node.Name && m.GetParameters().Length == parameterTypes.Length);

        foreach (MethodInfo candidate in candidates)
        {
            // Parameter count alone does not identify an overload. Overloads routinely differ
            // only in generic arity — Include<TEntity>(IQueryable<TEntity>, string) vs
            // Include<TEntity, TProperty>(IQueryable<TEntity>, Expression<…>) both take two
            // parameters — so arity must be checked before MakeGenericMethod, and a mismatch
            // must skip the candidate rather than throw past the remaining ones.
            if (candidate.IsGenericMethodDefinition
                != (genericArguments.Length > 0))
            {
                continue;
            }

            MethodInfo method;
            if (candidate.IsGenericMethodDefinition)
            {
                if (candidate.GetGenericArguments().Length != genericArguments.Length)
                {
                    continue;
                }

                try
                {
                    method = candidate.MakeGenericMethod(genericArguments);
                }
                catch (ArgumentException)
                {
                    // Generic constraints not satisfied — a different overload is meant.
                    continue;
                }
            }
            else
            {
                method = candidate;
            }

            if (!method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameterTypes))
            {
                continue;
            }

            // Name and parameters do not always identify an overload. Conversion operators are
            // the case that bites: `decimal` declares op_Explicit(decimal) returning double,
            // int, long, … — every one identical but for its return type. Picking the wrong one
            // produced "the operands for operator 'Convert' do not match the parameters of
            // method 'op_Explicit'". Prefer an exact return-type match, and fall back to the
            // first signature match so a TypeNode we cannot resolve does not lose the method.
            if (method.ReturnType == returnType)
            {
                return Admit(method, declaringType, node);
            }

            signatureMatch ??= method;
        }

        return signatureMatch is { } fallback
            ? Admit(fallback, declaringType, node)
            : throw new InvalidOperationException($"Method '{node.Name}' not resolvable on '{declaringType}'.");
    }

    /// <summary>
    ///     The method allowlist gate (ADR-008 constraint 2, milestone M5): a payload may name a
    ///     <b>public</b> method on an allowed type, or one of the few non-public methods EF's own
    ///     query rewrites produce.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The type allowlist has always run first, and it is broad by necessity — every
    ///         entity type, every mapped property type, every declaring type in the model. Binding
    ///         with <see cref="BindingFlags.NonPublic" /> on top of that let a payload name any
    ///         private method on any of them, which is a far larger surface than anything a caller
    ///         could have written, in a component whose entire job is to deserialize expression
    ///         trees from a remote client.
    ///     </para>
    ///     <para>
    ///         Non-public methods are still <em>looked up</em> — the marker overloads have to be
    ///         findable — and then refused here unless named. That ordering is what makes the rule
    ///         expressible at all: C25 measured "just drop <c>NonPublic</c>" at <b>154 → 697</b>.
    ///     </para>
    /// </remarks>
    private static MethodInfo Admit(MethodInfo method, Type declaringType, MethodNode node)
    {
        MethodInfo definition = method.IsGenericMethod && !method.IsGenericMethodDefinition
            ? method.GetGenericMethodDefinition()
            : method;

        if (definition.IsPublic
            || AllowedNonPublicMethods.Contains((declaringType.FullName ?? string.Empty, definition.Name)))
        {
            return method;
        }

        throw new InvalidOperationException(
            $"Method '{node.Name}' on '{declaringType}' is not public and is not on the "
            + "deserialization method allowlist (ADR-008 constraint 2). A payload may name a public "
            + "method on an allowed type; the only non-public methods admitted are the marker "
            + "overloads EF's own query rewrites produce.");
    }

    private Expression TranslateLambda(LambdaNode node)
    {
        // Resolve the declared parameters first so body references to them resolve to the very
        // same ParameterExpression instances (TranslateParameter caches by id).
        var parameters = node.Parameters.Select(TranslateParameter).ToList();

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
            // A value type declares no parameterless constructor, so `GetConstructor` returns
            // null for `new DateTime()` or `new MyStruct()` even though the expression is
            // perfectly legal. `Expression.New(Type)` is the overload for exactly that case.
            if (arguments.Length == 0 && type.IsValueType)
            {
                return Expression.New(type);
            }

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
        ExpressionType op = ParseOperator(node.Operator, AllowedBinaryOperators, nameof(BinaryNode));
        MethodInfo? method = node.Method is null ? null : ResolveMethod(node.Method);
        return Expression.MakeBinary(op, left, right, node.IsLiftedToNull, method);
    }

    private Expression TranslateUnary(UnaryNode node)
    {
        Expression operand = TranslateNode(node.Operand);
        ExpressionType op = ParseOperator(node.Operator, AllowedUnaryOperators, nameof(UnaryNode));
        Type type = _typeResolver.Resolve(node.Type);
        MethodInfo? method = node.Method is null ? null : ResolveMethod(node.Method);
        return Expression.MakeUnary(op, operand, type, method);
    }

    private Expression TranslateTypeBinary(TypeBinaryNode node)
    {
        Expression operand = TranslateNode(node.Operand);
        Type typeOperand = _typeResolver.Resolve(node.TypeOperand);
        return ParseOperator(node.Operator, AllowedTypeBinaryOperators, nameof(TypeBinaryNode)) == ExpressionType.TypeEqual
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

    /// <summary>
    ///     The operator kinds a <see cref="BinaryNode" /> may name (ADR-008 constraint 2, the
    ///     node-kind third of it).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Every value <see cref="Expression.MakeBinary(ExpressionType, Expression, Expression)" />
    ///         accepts that is <em>pure</em> — the assignment forms (<c>Assign</c>, <c>AddAssign</c>
    ///         and the twenty-five others) are the whole of what is left out, and they are left out
    ///         because a C# expression-tree lambda cannot contain one. That restriction is what
    ///         makes this list derivable rather than guessed: the forward translator emits
    ///         <c>node.NodeType.ToString()</c> off a live <see cref="BinaryExpression" />, and the
    ///         only live binary expressions a client can have built are the ones the compiler and
    ///         EF's own query construction produce.
    ///     </para>
    /// </remarks>
    private static readonly Dictionary<string, ExpressionType> AllowedBinaryOperators = ByName(
    [
        ExpressionType.Add, ExpressionType.AddChecked,
        ExpressionType.Subtract, ExpressionType.SubtractChecked,
        ExpressionType.Multiply, ExpressionType.MultiplyChecked,
        ExpressionType.Divide, ExpressionType.Modulo, ExpressionType.Power,
        ExpressionType.And, ExpressionType.AndAlso,
        ExpressionType.Or, ExpressionType.OrElse,
        ExpressionType.ExclusiveOr,
        ExpressionType.LeftShift, ExpressionType.RightShift,
        ExpressionType.Equal, ExpressionType.NotEqual,
        ExpressionType.GreaterThan, ExpressionType.GreaterThanOrEqual,
        ExpressionType.LessThan, ExpressionType.LessThanOrEqual,
        ExpressionType.Coalesce, ExpressionType.ArrayIndex,
    ]);

    /// <summary>
    ///     The operator kinds a <see cref="UnaryNode" /> may name. The pure subset again:
    ///     <c>Throw</c> and the pre/post increment-assign forms are what is excluded, for the same
    ///     reason — no expression-tree lambda can carry one.
    /// </summary>
    private static readonly Dictionary<string, ExpressionType> AllowedUnaryOperators = ByName(
    [
        ExpressionType.Negate, ExpressionType.NegateChecked, ExpressionType.UnaryPlus,
        ExpressionType.Not, ExpressionType.OnesComplement,
        ExpressionType.IsTrue, ExpressionType.IsFalse,
        ExpressionType.Convert, ExpressionType.ConvertChecked,
        ExpressionType.TypeAs, ExpressionType.Unbox,
        ExpressionType.Quote, ExpressionType.ArrayLength,
        ExpressionType.Increment, ExpressionType.Decrement,
    ]);

    /// <summary>
    ///     The two operator kinds a <see cref="TypeBinaryNode" /> may name.
    /// </summary>
    /// <remarks>
    ///     Naming them is what makes <see cref="TranslateTypeBinary" />'s two-way choice honest:
    ///     it reads "TypeEqual, else TypeIs", which without this list would quietly turn any of
    ///     the other eighty-three <see cref="ExpressionType" /> names into a <c>TypeIs</c>.
    /// </remarks>
    private static readonly Dictionary<string, ExpressionType> AllowedTypeBinaryOperators = ByName(
    [
        ExpressionType.TypeIs, ExpressionType.TypeEqual,
    ]);

    private static Dictionary<string, ExpressionType> ByName(ExpressionType[] operators)
        => operators.ToDictionary(op => op.ToString(), StringComparer.Ordinal);

    /// <summary>
    ///     Parses and admits a wire-supplied operator name against the allowlist for the node kind
    ///     that carries it (ADR-008 constraint 2, milestone M5).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The node <em>kinds</em> themselves are closed by construction — <c>$kind</c> is a
    ///         System.Text.Json polymorphic discriminator over the fifteen
    ///         <c>[JsonDerivedType]</c>s registered on <see cref="ExpressionNode" />, and an
    ///         unregistered value fails deserialization before any node exists;
    ///         <see cref="ExpressionNode.Kind" /> is <c>[JsonIgnore]</c> and answered by the CLR
    ///         type, so it is never wire-supplied. The operator is the part that <em>was</em>
    ///         open: it is a free string, and <see cref="Enum.TryParse{T}(string, out T)" /> admits
    ///         far more than the names this wire emits — every one of the eighty-five
    ///         <see cref="ExpressionType" /> members including <c>Assign</c> and <c>Throw</c>,
    ///         bare numeric strings such as <c>"999"</c>, and comma-separated combinations, which
    ///         it accepts whether or not the enum is <c>[Flags]</c>.
    ///     </para>
    ///     <para>
    ///         <c>Assign</c> reaching <see cref="Expression.MakeBinary(ExpressionType, Expression, Expression)" />
    ///         is the case that makes this more than tidiness: it builds a mutation into a tree the
    ///         server is about to compile and run. The DTOs have claimed since they were written
    ///         that the operator is "an explicit map, never an int-cast"; this is where that
    ///         becomes true.
    ///     </para>
    /// </remarks>
    private static ExpressionType ParseOperator(
        string name, Dictionary<string, ExpressionType> allowed, string nodeKind)
    {
        // An ordinal name lookup in the allowlist, never Enum.TryParse against the whole enum:
        // TryParse would accept "999" and "Add, Not" as readily as it accepts "Assign".
        if (allowed.TryGetValue(name, out ExpressionType result))
        {
            return result;
        }

        throw new NotSupportedException(
            $"Operator '{name}' is not on the deserialization operator allowlist for {nodeKind} "
            + "(ADR-008 constraint 2). A payload may name a pure operator of that node kind; the "
            + "assignment and control-flow forms are refused because no expression-tree lambda "
            + "can contain one.");
    }
}
