// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;

namespace InfoCarrier.Core.Query;

/// <summary>
///     Reports the CLR types a single expression node would put on the wire
///     (<c>docs/projection-split.md</c> §3.1) — the input to the server-boundary analysis.
/// </summary>
/// <remarks>
///     <para>
///         Each case mirrors one <c>_typeMapper.ToTypeNode(…)</c> call site in
///         <see cref="ExpressionToNodeTranslator" />. <b>Keep the two in step:</b> a type the
///         translator writes but this does not report is a type the client will ship and the
///         server will refuse. The mirroring is asserted by
///         <c>WireTypeCollectorTest</c>, and <c>QuerySplitter</c>'s verification pass
///         (§3.1) makes an omission recoverable rather than fatal.
///     </para>
///     <para>
///         Only the node's <em>own</em> types are reported, never its children's — the analysis
///         is bottom-up, so a caller walking the tree sees each node's contribution exactly once.
///     </para>
/// </remarks>
public static class WireTypeCollector
{
    /// <summary>
    ///     The types <paramref name="node" /> itself contributes to the wire payload.
    /// </summary>
    public static IReadOnlyList<Type> CollectOwn(Expression node)
    {
        var types = new List<Type>();
        CollectOwn(node, types);
        return types;
    }

    /// <summary>
    ///     Adds the types <paramref name="node" /> itself contributes to <paramref name="into" />.
    /// </summary>
    public static void CollectOwn(Expression node, ICollection<Type> into)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(into);

        // Every node writes its own Type, query-root stubs included — `QueryRootStubNode` carries
        // both an ElementType and a Type.
        //
        // A *constant* is the one node whose `Type` is a runtime type rather than a declared one:
        // EF's funcletizer types it by the value it holds, so `IPAddress.Loopback` arrives typed
        // as the private `IPAddress+ReadOnlyIPAddress`. `TypeNodeMapper.Nameable` reports what the
        // wire will actually name, which is what this analysis has to ask the allowlist about —
        // asking about the raw type refused a subtree over a name that would never be sent (B23).
        // Confined to constants on purpose; see the remarks on `Nameable` for the 375 that a
        // general application of it cost.
        into.Add(node is ConstantExpression ? Expressions.TypeNodeMapper.Nameable(node.Type) : node.Type);

        switch (node)
        {
            case ConstantExpression constant:
                if (constant.Value is IQueryable queryable)
                {
                    // A stub carries only its element type, which the server resolves through
                    // its own model rather than by name.
                    into.Add(queryable.ElementType);
                    break;
                }

                // A boxed value travels through the dynamic-value graph, which writes the
                // *runtime* type, not the declared one. `object x = new ClientDto()` is a
                // client-only type on the wire even though the node's Type is `object`.
                if (constant.Value is { } value)
                {
                    into.Add(Expressions.TypeNodeMapper.Nameable(value.GetType()));
                }

                break;

            case Microsoft.EntityFrameworkCore.Query.QueryRootExpression root:
                into.Add(root.ElementType);
                break;

            case MemberExpression member:
                AddDeclaringType(member.Member, into);
                break;

            case MethodCallExpression call:
                AddMethod(call.Method, into);
                break;

            case LambdaExpression lambda:
                into.Add(lambda.ReturnType);
                foreach (ParameterExpression parameter in lambda.Parameters)
                {
                    into.Add(parameter.Type);
                }

                break;

            case NewExpression @new:
                if (@new.Constructor is { } constructor)
                {
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                    {
                        into.Add(parameter.ParameterType);
                    }
                }

                break;

            case BinaryExpression binary:
                if (binary.Method is { } binaryMethod)
                {
                    AddMethod(binaryMethod, into);
                }

                break;

            case UnaryExpression unary:
                if (unary.Method is { } unaryMethod)
                {
                    AddMethod(unaryMethod, into);
                }

                break;

            case TypeBinaryExpression typeBinary:
                into.Add(typeBinary.TypeOperand);
                break;

            case MemberInitExpression memberInit:
                foreach (MemberBinding binding in memberInit.Bindings)
                {
                    if (binding.BindingType == MemberBindingType.Assignment)
                    {
                        AddDeclaringType(binding.Member, into);
                    }
                }

                break;

            case ListInitExpression listInit:
                foreach (ElementInit initializer in listInit.Initializers)
                {
                    AddMethod(initializer.AddMethod, into);
                }

                break;

            // Parameter, NewArray, Conditional and Invocation contribute nothing beyond
            // node.Type, already added above.
            default:
                break;
        }
    }

    private static void AddDeclaringType(MemberInfo member, ICollection<Type> into)
    {
        if (member.DeclaringType is { } declaringType)
        {
            into.Add(declaringType);
        }
    }

    private static void AddMethod(MethodInfo method, ICollection<Type> into)
    {
        AddDeclaringType(method, into);
        into.Add(method.ReturnType);

        foreach (ParameterInfo parameter in method.GetParameters())
        {
            into.Add(parameter.ParameterType);
        }

        if (method.IsGenericMethod)
        {
            foreach (Type argument in method.GetGenericArguments())
            {
                into.Add(argument);
            }
        }
    }
}
