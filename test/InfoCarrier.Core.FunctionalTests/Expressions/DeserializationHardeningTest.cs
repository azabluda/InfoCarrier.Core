// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Expressions;

/// <summary>
///     The adversarial half of M5's security review (<c>docs/security-review.md</c>): payloads
///     that a hostile client would send, asserted to be refused.
/// </summary>
/// <remarks>
///     <para>
///         The allowlists are tested elsewhere for what they <em>admit</em>. This asserts the
///         thing a review actually has to establish: that the admitted set cannot be composed
///         into a pivot. Every case here is a chain that starts from something legitimately
///         allowlisted and tries to reach code execution through it.
///     </para>
///     <para>
///         These are executable claims rather than prose. A review whose conclusions are only
///         written down goes stale the first time someone adds a type to a list.
///     </para>
/// </remarks>
public class DeserializationHardeningTest
{
    private static NodeToExpressionTranslator Translator()
        => new(
            new TypeNodeResolver(),
            new DynamicValueMapper(null, new TypeNodeMapper(), new TypeNodeResolver()),
            (stub, type) => throw new NotSupportedException("No query roots here."));

    private static TypeNode Type(Type type) => new() { Name = type.FullName! };

    private static ConstantNode Constant<T>(T value)
        => new()
        {
            Type = Type(typeof(T)),
            PrimitiveValue = value,
        };

    /// <summary>
    ///     A type the model does not know cannot be named, which is the invariant every other
    ///     refusal below rests on.
    /// </summary>
    [Theory]
    [InlineData("System.Diagnostics.Process")]
    [InlineData("System.IO.File")]
    [InlineData("System.Reflection.Assembly")]
    [InlineData("System.AppDomain")]
    [InlineData("System.Activator")]
    public void A_type_outside_the_allowlist_cannot_be_named(string typeName)
    {
        var node = new MemberNode
        {
            DeclaringType = new TypeNode { Name = typeName },
            MemberName = "Anything",
            MemberKind = MemberKind.Property,
            Type = Type(typeof(object)),
        };

        Assert.ThrowsAny<Exception>(() => Translator().Translate(node));
    }

    /// <summary>
    ///     <c>System.Type</c> <em>is</em> allowlisted, and it is the most dangerous thing on the
    ///     list: <c>Type.GetType(string)</c> would hand a payload a type the allowlist never saw,
    ///     at run time on the server, after every deserialization-time check has passed.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         The reason that is not a hole is worth stating precisely, because it is not
    ///         obvious and it is load-bearing: <b>a <c>Type</c> obtained this way has nothing to
    ///         call.</b> Every reflection entry point that would turn it into an invocation takes
    ///         a parameter, or lives on a type, that the allowlist does not admit —
    ///         <c>Type.InvokeMember</c> needs a <c>System.Reflection.Binder</c>,
    ///         <c>MethodInfo.Invoke</c> needs <c>MethodBase</c> as its declaring type, and
    ///         <c>Activator</c> is not admitted at all. <c>ResolveMethod</c> resolves a method's
    ///         parameter types through the same allowlist, so an unadmitted parameter type fails
    ///         the signature lookup before <c>Admit</c> is even consulted.
    ///     </para>
    ///     <para>
    ///         <b>So the bound is a conjunction, not a single check</b>, and adding
    ///         <c>Binder</c>, <c>MethodBase</c>, <c>MethodInfo</c> or <c>Activator</c> to
    ///         <c>TypeAllowlist</c> would break it. That is exactly what this test is for.
    ///     </para>
    /// </remarks>
    [Theory]
    [InlineData("System.Reflection.Binder")]
    [InlineData("System.Reflection.MethodBase")]
    [InlineData("System.Reflection.MethodInfo")]
    [InlineData("System.Reflection.ConstructorInfo")]
    [InlineData("System.Reflection.PropertyInfo")]
    [InlineData("System.Activator")]
    [InlineData("System.AppDomain")]
    [InlineData("System.Reflection.Assembly")]
    public void The_reflection_types_that_would_turn_a_Type_into_a_call_are_not_admitted(string typeName)
    {
        var resolver = new TypeNodeResolver();

        Assert.ThrowsAny<Exception>(() => resolver.Resolve(new TypeNode { Name = typeName }));
    }

    /// <summary>
    ///     <c>BindingFlags</c> <em>is</em> admitted, and that is deliberate: <c>TypeAllowlist</c>
    ///     ends with <c>return type.IsEnum</c>, on the ground that an enum is data rather than
    ///     behaviour and travels as its underlying value anyway.
    /// </summary>
    /// <remarks>
    ///     Recorded rather than fixed, because it is sound and the review should say why. An enum
    ///     constructs nothing on its own; what it can do is complete a <em>signature</em>, which
    ///     is how it appears in <c>Type.InvokeMember(string, BindingFlags, Binder, …)</c>. That
    ///     overload is still unreachable, and the reason is the <c>Binder</c> above, not this.
    ///     The distinction matters: someone hardening this later should not spend effort on
    ///     enums.
    /// </remarks>
    [Fact]
    public void Any_enum_is_admitted_including_BindingFlags_and_that_is_not_the_bound()
    {
        var resolver = new TypeNodeResolver();

        Assert.Equal(
            typeof(BindingFlags),
            resolver.Resolve(new TypeNode { Name = typeof(BindingFlags).FullName! }));
    }

    /// <summary>
    ///     And the concrete pivot, spelled out end to end:
    ///     <c>Type.GetType("System.Diagnostics.Process").InvokeMember("Start", …)</c>.
    /// </summary>
    [Fact]
    public void The_Type_GetType_then_InvokeMember_pivot_is_refused()
    {
        // `Type.GetType(string)` on its own resolves — `System.Type` is allowlisted and the
        // method is public. That is the honest starting point, not a straw man.
        var getType = new MethodCallNode
        {
            Method = new MethodNode
            {
                DeclaringType = Type(typeof(Type)),
                Name = nameof(System.Type.GetType),
                ParameterTypes = [Type(typeof(string))],
                ReturnType = Type(typeof(Type)),
                GenericArguments = [],
            },
            Arguments = [Constant("System.Diagnostics.Process")],
            Type = Type(typeof(Type)),
        };

        Assert.NotNull(Translator().Translate(getType));

        // The step that would matter cannot be expressed: every `InvokeMember` overload takes a
        // `System.Reflection.Binder`, and that type cannot be named.
        var invokeMember = new MethodCallNode
        {
            Method = new MethodNode
            {
                DeclaringType = Type(typeof(Type)),
                Name = nameof(System.Type.InvokeMember),
                ParameterTypes =
                [
                    Type(typeof(string)),
                    new TypeNode { Name = "System.Reflection.BindingFlags" },
                    new TypeNode { Name = "System.Reflection.Binder" },
                    Type(typeof(object)),
                    new TypeNode { Name = "System.Object[]" },
                ],
                ReturnType = Type(typeof(object)),
                GenericArguments = [],
            },
            Instance = getType,
            Arguments = [Constant("Start"), Constant(0), Constant<object?>(null), Constant<object?>(null), Constant<object?>(null)],
            Type = Type(typeof(object)),
        };

        Assert.ThrowsAny<Exception>(() => Translator().Translate(invokeMember));
    }

    /// <summary>
    ///     A non-public method on an allowed type is refused by name (C30), so the marker
    ///     exception list cannot be widened by accident.
    /// </summary>
    [Fact]
    public void A_non_public_method_on_an_allowed_type_is_refused()
    {
        MethodInfo target = typeof(string).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .First(m => m.GetParameters().Length == 1 && !m.IsGenericMethodDefinition);

        var node = new MethodCallNode
        {
            Method = new MethodNode
            {
                DeclaringType = Type(typeof(string)),
                Name = target.Name,
                ParameterTypes = [Type(target.GetParameters()[0].ParameterType)],
                ReturnType = Type(target.ReturnType),
                GenericArguments = [],
            },
            Arguments = [Constant("x")],
            Type = Type(typeof(object)),
        };

        Assert.ThrowsAny<Exception>(() => Translator().Translate(node));
    }

    /// <summary>
    ///     A <c>NewNode</c> constructs only an allowlisted type. This is the one node kind that
    ///     runs a constructor at deserialization time rather than at query time, so it is the one
    ///     worth naming separately.
    /// </summary>
    [Fact]
    public void A_new_expression_cannot_name_an_unadmitted_type()
    {
        var node = new NewNode
        {
            Type = new TypeNode { Name = "System.IO.FileStream" },
            Arguments = [],
            ConstructorParameterTypes = [],
        };

        Assert.ThrowsAny<Exception>(() => Translator().Translate(node));
    }

    /// <summary>
    ///     The depth bound is real and is the reason a deeply nested payload cannot exhaust the
    ///     stack in the translator's recursion.
    /// </summary>
    [Fact]
    public void The_serializer_context_bounds_nesting_depth()
    {
        // Deeper than ExpressionJsonContext's MaxDepth of 256.
        string json = string.Concat(Enumerable.Repeat("""{"$kind":8,"operator":"Not","type":{"name":"System.Boolean"},"operand":""", 400))
            + """{"$kind":0,"type":{"name":"System.Boolean"},"primitiveValue":true}"""
            + new string('}', 400);

        Assert.ThrowsAny<Exception>(
            () => System.Text.Json.JsonSerializer.Deserialize(
                json, ExpressionJsonContext.Default.ExpressionNode));
    }
}
