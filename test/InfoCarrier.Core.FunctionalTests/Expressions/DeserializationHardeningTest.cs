// Licensed under the MIT license. See license.txt file in the project root for license information.

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
    ///     <c>Regex</c> is admitted (M9 J20, reversing A46), and the member that would matter —
    ///     <c>Regex.CompileToAssembly</c>, which writes an assembly — cannot be named.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Why this test exists rather than a paragraph.</b> §2's bound is a conjunction
    ///         over the reflection invocation surface, and admitting a type is precisely how a
    ///         conjunction breaks. `Regex` is on none of that surface — but it still carries one
    ///         member that reaches it, so the claim "admitting `Regex` is safe" needs the same
    ///         treatment `Binder` gets: an executable assertion.
    ///     </para>
    ///     <para>
    ///         <b>And it is blocked by §2's own mechanism, not by a special case.</b>
    ///         <c>ResolveMethod</c> resolves every parameter type through the same allowlist, and
    ///         <c>CompileToAssembly</c> takes <c>RegexCompilationInfo[]</c>,
    ///         <c>System.Reflection.AssemblyName</c> and
    ///         <c>System.Reflection.Emit.CustomAttributeBuilder[]</c>. None is admitted, so the
    ///         signature lookup fails before the method is found — exactly how <c>Binder</c>
    ///         blocks <c>Type.InvokeMember</c> above.
    ///     </para>
    ///     <para>
    ///         Deliberately <em>not</em> resting on the fact that the method throws
    ///         <c>PlatformNotSupportedException</c> on modern .NET. That is true and it is the
    ///         weaker argument: it would stop being a reason if the runtime ever changed, whereas
    ///         the signature argument is a property of this allowlist.
    ///     </para>
    /// </remarks>
    [Fact]
    public void Regex_is_admitted_but_CompileToAssembly_cannot_be_named()
    {
        var resolver = new TypeNodeResolver();

        // The premise: `Regex` itself resolves, and so does the call the query actually needs.
        Assert.NotNull(resolver.Resolve(Type(typeof(System.Text.RegularExpressions.Regex))));

        var isMatch = new MethodCallNode
        {
            Method = new MethodNode
            {
                DeclaringType = Type(typeof(System.Text.RegularExpressions.Regex)),
                Name = nameof(System.Text.RegularExpressions.Regex.IsMatch),
                ParameterTypes = [Type(typeof(string)), Type(typeof(string))],
                ReturnType = Type(typeof(bool)),
                GenericArguments = [],
            },
            Arguments = [Constant("Seattle"), Constant("^S")],
            Type = Type(typeof(bool)),
        };

        Assert.NotNull(Translator().Translate(isMatch));

        // Each of the three parameter types on its own.
        foreach (string blocked in new[]
                 {
                     "System.Reflection.AssemblyName",
                     "System.Reflection.Emit.CustomAttributeBuilder",
                     "System.Text.RegularExpressions.RegexCompilationInfo",
                 })
        {
            Assert.ThrowsAny<Exception>(() => resolver.Resolve(new TypeNode { Name = blocked }));
        }

        // And the whole call, which is what a payload would actually send.
        var compileToAssembly = new MethodCallNode
        {
            Method = new MethodNode
            {
                DeclaringType = Type(typeof(System.Text.RegularExpressions.Regex)),
                Name = "CompileToAssembly",
                ParameterTypes =
                [
                    new TypeNode { Name = "System.Text.RegularExpressions.RegexCompilationInfo[]" },
                    new TypeNode { Name = "System.Reflection.AssemblyName" },
                ],
                ReturnType = Type(typeof(void)),
                GenericArguments = [],
            },
            Arguments = [Constant<object?>(null), Constant<object?>(null)],
            Type = Type(typeof(void)),
        };

        Assert.ThrowsAny<Exception>(() => Translator().Translate(compileToAssembly));
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

    // ---- C53's widening, bounded ------------------------------------------------------------

    /// <summary>
    ///     C53 admits the base classes of a mapped property's CLR type. These pin what that adds,
    ///     against a real model rather than the model-free allowlist the tests above use.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The method-reachability delta is nil, and that is the load-bearing fact.</b>
    ///         <c>ResolveMethod</c> calls <c>declaringType.GetMethods(flags)</c> without
    ///         <c>BindingFlags.DeclaredOnly</c>, so inherited public methods are found by naming
    ///         the <em>derived</em> type. Everything public on a base was callable before C53 by
    ///         saying the subclass; admitting the base adds no method.
    ///     </para>
    ///     <para>
    ///         What it adds is the ability to <em>name</em> and <em>construct</em> the base — and
    ///         that base is, by construction, a base of something the application itself mapped.
    ///     </para>
    /// </remarks>
    [Fact]
    public void A_mapped_propertys_base_class_is_admitted_but_a_category_is_not()
    {
        using var context = new BaseChainContext();
        TypeAllowlist allowlist = TypeAllowlist.ForModel(context.Model);

        Assert.True(allowlist.IsAllowed(typeof(MiddleThing)), "the intermediate base is admitted");
        Assert.True(allowlist.IsAllowed(typeof(RootThing)), "and the one above it");

        // The category stays out: `int`'s base is `ValueType`, and C23 measured widening to one
        // of these at 145 -> 186. Every value-typed property in every model would otherwise put
        // it on the list.
        Assert.False(allowlist.IsAllowed(typeof(ValueType)));

        // `Delegate` *is* allowed, by a rule that predates C53 and is not affected by it: the
        // branch that admits `Func<,>` so a lambda can travel reaches `typeof(Delegate)` itself,
        // because `Delegate.IsAssignableFrom(Delegate)`. Harmless — it is abstract and
        // constructs nothing — and asserted here so the next reader does not mistake it for
        // something the base-class rule did.
        Assert.True(allowlist.IsAllowed(typeof(Delegate)));
    }

    /// <summary>
    ///     The element type of a mapped collection property is admitted only when the application
    ///     registers it (<c>security-review.md</c> §2b).
    /// </summary>
    /// <remarks>
    ///     A list stored in one column through a converter names nothing about its element that the
    ///     application vouched for. Admitting the element by inference made every public method of
    ///     a framework type such as <see cref="FileInfo" /> reachable from a payload, and the server
    ///     evaluates a closed subtree before it translates the query.
    /// </remarks>
    [Fact]
    public void A_mapped_collection_propertys_element_type_is_admitted_only_when_registered()
    {
        using var context = new FileListContext();

        Assert.False(TypeAllowlist.ForModel(context.Model).IsAllowed(typeof(FileInfo)));
        Assert.True(TypeAllowlist.ForModel(context.Model, [typeof(FileInfo)]).IsAllowed(typeof(FileInfo)));
    }

    /// <summary>
    ///     The <c>Type</c> clause admits three names, and a query naming a type needs all three.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <c>security-review.md</c> §2 named <see cref="System.Type" /> "and everything
    ///         assignable to it" until 2026-09-23, which reads like one entry and is three. A
    ///         <c>typeof(X)</c> constant makes the boundary ask about all of them:
    ///         <see cref="System.Type" /> for the constant's declared type,
    ///         <see cref="System.Reflection.TypeInfo" /> because <c>TypeNodeMapper.Nameable</c>
    ///         maps the value's runtime type to its first <em>visible</em> base, and
    ///         <c>System.RuntimeType</c> on the server, which is internal and reachable only as
    ///         <c>typeof(int).GetType()</c>.
    ///     </para>
    ///     <para>
    ///         <b>This is why registration cannot stand in for the clause</b>, measured
    ///         2026-09-22: <c>_allowed</c> is an exact-match set and admits one name per entry,
    ///         so an application registering <see cref="System.Type" /> alone still had its
    ///         query refused. Registering all three restored it, and one of the three can only
    ///         be written <c>typeof(int).GetType()</c>. §2's addendum (2) carries the reading.
    ///     </para>
    /// </remarks>
    [Fact]
    public void The_Type_clause_admits_the_three_names_a_typeof_value_reaches()
    {
        TypeAllowlist allowlist = TypeAllowlist.ForModel(null);

        Assert.True(allowlist.IsAllowed(typeof(System.Type)), "the constant's declared type");
        Assert.True(allowlist.IsAllowed(typeof(TypeInfo)), "what Nameable reports for the value");
        Assert.True(allowlist.IsAllowed(typeof(int).GetType()), "System.RuntimeType, on the server");

        // The neighbours stay out, which is what makes this a clause about `Type` rather than
        // about reflection: `MemberInfo` is `Type`'s own base and is refused.
        Assert.False(allowlist.IsAllowed(typeof(MemberInfo)));
        Assert.False(allowlist.IsAllowed(typeof(MethodInfo)));

        // AND THE RUNTIME TYPE-BUILDING FAMILY STAYS OUT, WHICH IS WHAT "THE THREE" BUYS.
        // The clause read `typeof(Type).IsAssignableFrom(type)` until 2026-09-23, and a rule is
        // wider than a list: every one of these derives from `TypeInfo`, so every one was
        // admissible as a NAME. Never a hole — a payload cannot obtain one, because each route
        // runs through `ModuleBuilder` or `AssemblyBuilder` and both are refused — but §2's
        // bound is a conjunction, and this is one clause it no longer has to lean on.
        Assert.False(allowlist.IsAllowed(typeof(System.Reflection.Emit.TypeBuilder)));
        Assert.False(allowlist.IsAllowed(typeof(System.Reflection.Emit.EnumBuilder)));
        Assert.False(allowlist.IsAllowed(typeof(System.Reflection.Emit.GenericTypeParameterBuilder)));
        Assert.False(allowlist.IsAllowed(typeof(TypeDelegator)));
    }

    /// <summary>
    ///     A property whose own CLR type is on the reflection invocation surface is admitted only
    ///     when the application registers it (<c>security-review.md</c> §2b).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A value converter can map any CLR type, <see cref="MethodInfo" /> included, so
    ///         "the model named it" is not by itself a reason to admit a type. It is the reason
    ///         to admit an <em>entity</em>, whose instances the model produces; a converted
    ///         property type is whatever the application wrote a converter for, and the payload
    ///         gets to name it afterwards. <c>ResolveMethod</c> finds inherited methods, so
    ///         admitting <see cref="MethodInfo" /> would put <c>Invoke</c> within reach.
    ///     </para>
    ///     <para>
    ///         <c>AddPropertyBaseTypes</c> has stopped at this surface since C53 — the base chain
    ///         <c>MethodBase</c>, <c>MemberInfo</c> was already refused while the property type
    ///         itself was not.
    ///     </para>
    /// </remarks>
    [Fact]
    public void A_property_mapped_as_a_reflection_type_is_admitted_only_when_registered()
    {
        using var context = new MethodPropertyContext();

        Assert.False(TypeAllowlist.ForModel(context.Model).IsAllowed(typeof(MethodInfo)));
        Assert.True(TypeAllowlist.ForModel(context.Model, [typeof(MethodInfo)]).IsAllowed(typeof(MethodInfo)));
    }

    /// <summary>
    ///     The conjunction from <c>security-review.md</c> §2 still holds under a
    ///     <em>model-derived</em> allowlist, which is the one a server actually runs. The theory
    ///     earlier in this file checks the model-free list, which C53 does not touch — so without
    ///     this the widening would be unpinned exactly where it applies.
    /// </summary>
    [Theory]
    [InlineData("System.Reflection.Binder")]
    [InlineData("System.Reflection.MethodBase")]
    [InlineData("System.Reflection.MethodInfo")]
    [InlineData("System.Reflection.ConstructorInfo")]
    [InlineData("System.Activator")]
    [InlineData("System.AppDomain")]
    [InlineData("System.Reflection.Assembly")]
    public void A_model_derived_allowlist_still_refuses_the_reflection_invocation_surface(string typeName)
    {
        using var context = new BaseChainContext();
        TypeAllowlist allowlist = TypeAllowlist.ForModel(context.Model);
        System.Type type = System.Type.GetType(typeName) ?? typeof(object).Assembly.GetType(typeName)!;

        Assert.NotNull(type);
        Assert.False(allowlist.IsAllowed(type));
    }

    private class RootThing
    {
        public string Name { get; set; } = string.Empty;
    }

    private class MiddleThing : RootThing;

    private sealed class LeafThing : MiddleThing;

    private sealed class BaseChainEntity
    {
        public int Id { get; set; }

        public LeafThing Leaf { get; set; } = new();
    }

    private sealed class BaseChainContext : Microsoft.EntityFrameworkCore.DbContext
    {
        protected override void OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder optionsBuilder)
            => Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions
                .UseInMemoryDatabase(optionsBuilder, "hardening-base-chain");

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
            => modelBuilder.Entity<BaseChainEntity>()
                .Property(e => e.Leaf)
                .HasConversion(v => v.Name, v => new LeafThing { Name = v });
    }

    private sealed class FileListEntity
    {
        public int Id { get; set; }

        public List<FileInfo> Files { get; set; } = [];
    }

    private sealed class FileListContext : Microsoft.EntityFrameworkCore.DbContext
    {
        protected override void OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder optionsBuilder)
            => Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions
                .UseInMemoryDatabase(optionsBuilder, "hardening-file-list");

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
            => modelBuilder.Entity<FileListEntity>()
                .Property(e => e.Files)
                .HasConversion(
                    v => string.Join("|", v.Select(f => f.FullName)),
                    v => v.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(p => new FileInfo(p)).ToList());
    }

    private sealed class MethodPropertyEntity
    {
        public int Id { get; set; }

        public MethodInfo Method { get; set; } = typeof(object).GetMethod(nameof(ToString))!;
    }

    private sealed class MethodPropertyContext : Microsoft.EntityFrameworkCore.DbContext
    {
        protected override void OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder optionsBuilder)
            => Microsoft.EntityFrameworkCore.InMemoryDbContextOptionsExtensions
                .UseInMemoryDatabase(optionsBuilder, "hardening-method-property");

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
            => modelBuilder.Entity<MethodPropertyEntity>()
                .Property(e => e.Method)
                .HasConversion(
                    v => v.Name,
                    v => typeof(object).GetMethod(v)!);
    }
}
