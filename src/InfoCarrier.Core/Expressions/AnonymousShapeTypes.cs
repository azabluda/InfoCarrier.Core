// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

namespace InfoCarrier.Core.Expressions;

// Only this factory's types are admitted on the server. CompilerGeneratedAttribute is not a grant.
internal static class AnonymousShapeTypes
{
    internal const string Prefix = "InfoCarrier.AnonymousShape.v1/";
    internal const int MaximumMembers = 32;
    internal const int MaximumShapes = 64;
    internal const int MaximumDepth = 16;
    private const int MaximumGeneratedTypes = 4096;
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Type> Types = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, TypeNode> Descriptors = [];
    private static readonly HashSet<Type> Definitions = [];
    private static readonly ConditionalWeakTable<Assembly, AssemblyScope> Scopes = new();
    private sealed class AssemblyScope
    {
        internal string Token { get; } = Guid.NewGuid().ToString("N");
    }

    private static int _emissions;

    internal static bool IsOriginal(Type type)
        => type.IsSealed && !type.IsVisible
            && type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
            && type.IsDefined(typeof(CompilerGeneratedAttribute), false);

    internal static bool IsGenerated(Type type)
    {
        lock (Gate)
        {
            return Descriptors.ContainsKey(type) || Definitions.Contains(type);
        }
    }

    internal static bool TryDescriptor(Type type, out TypeNode? descriptor)
    {
        lock (Gate)
        {
            if (Descriptors.TryGetValue(type, out TypeNode? saved))
            {
                descriptor = Snapshot(saved);
                return true;
            }

            descriptor = null;
            return false;
        }
    }

    internal static string Identity(Type type)
        => Prefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            (type.IsGenericType ? type.GetGenericTypeDefinition() : type).AssemblyQualifiedName
                + "|" + type.Module.ModuleVersionId + "|"
                + (AssemblyLoadContext.GetLoadContext(type.Assembly) == AssemblyLoadContext.Default
                    ? "default" : Scopes.GetValue(type.Assembly, _ => new AssemblyScope()).Token))));

    internal static void Validate(TypeNode node)
    {
        if (!node.Name.StartsWith(Prefix, StringComparison.Ordinal)
            || node.Name.Length != Prefix.Length + 64
            || !node.Name.AsSpan(Prefix.Length).ToString().All(Uri.IsHexDigit)
            || node.EntityTypeName is not null || node.ArrayElement is not null || node.ArrayRank != 0
            || node.ShapeMembers is not { } names
            || names.Count > MaximumMembers || names.Count != node.GenericArguments.Count
            || names.Distinct(StringComparer.Ordinal).Count() != names.Count
            || names.Any(n => !ValidMemberName(n)))
        {
            throw new InvalidOperationException("Invalid bounded anonymous-shape descriptor: "
                + string.Join(",", node.ShapeMembers ?? []) + ".");
        }
    }

    private static bool ValidMemberName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 128)
        {
            return false;
        }

        // The C# compiler puts this reserved member into nested query-syntax carriers.
        // It is an IL metadata name, never source text or a requested executable method.
        const string transparent = "<>h__TransparentIdentifier";
        if (name.StartsWith(transparent, StringComparison.Ordinal))
        {
            return name.Length > transparent.Length && name.AsSpan(transparent.Length).ToString().All(char.IsAsciiDigit);
        }

        return IdentifierStart(name[0]) && name.All(c => IdentifierStart(c)
            || char.GetUnicodeCategory(c) is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format);
    }

    private static bool IdentifierStart(char value)
        => value == '_' || char.IsLetter(value) || char.GetUnicodeCategory(value) == UnicodeCategory.LetterNumber;

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The emitted data members and framework equality methods are generated and used together at runtime.")]
    [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "EqualityComparer<T> is instantiated only for admitted shape components; Native AOT is unsupported.")]
    internal static Type Resolve(TypeNode node, Type[] arguments)
    {
        Validate(node);
        if (arguments.Any(t => t == typeof(void) || t.IsByRef || t.IsPointer || t.IsByRefLike
            || t.IsFunctionPointer || t.ContainsGenericParameters))
        {
            throw new InvalidOperationException("Anonymous-shape components must be closed data types.");
        }

        string key = node.CacheIdentity() + string.Concat(node.ShapeMembers!.Zip(arguments,
            (name, type) => $"|{name.Length}:{name}:{type.TypeHandle.Value}"));
        lock (Gate)
        {
            if (Types.TryGetValue(key, out Type? existing))
            {
                return existing;
            }

            if (_emissions >= MaximumGeneratedTypes)
            {
                throw new InvalidOperationException("The process anonymous-shape generation budget is exhausted.");
            }

            // Count attempts before emission. A failed runtime type construction must not
            // permit an unbounded sequence of newly allocated collectible assemblies.
            _emissions++;
            Type generated = Emit(node.ShapeMembers!, arguments);
            Definitions.Add(generated.IsGenericType ? generated.GetGenericTypeDefinition() : generated);
            Types.Add(key, generated);
            Descriptors.Add(generated, Snapshot(node));
            return generated;
        }
    }

    private static TypeNode Snapshot(TypeNode node)
        => node with
        {
            ShapeMembers = node.ShapeMembers?.ToArray(),
            GenericArguments = node.GenericArguments.Select(Snapshot).ToArray(),
            ArrayElement = node.ArrayElement is null ? null : Snapshot(node.ArrayElement),
        };

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "The fixed emitter calls only framework EqualityComparer<T> members; original application methods are never emitted.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "These generic definitions are emitted at runtime with unconstrained parameters and have no trim-time member requirements.")]
    private static Type Emit(IReadOnlyList<string> names, Type[] types)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("InfoCarrier.AnonymousShapes." + Types.Count), AssemblyBuilderAccess.RunAndCollect);
        TypeBuilder builder = assembly.DefineDynamicModule("Shapes").DefineType(
            "<>f__AnonymousType" + Types.Count + (types.Length == 0 ? string.Empty : "`" + types.Length),
            TypeAttributes.Public | TypeAttributes.Sealed);
        Type[] parameters = types.Length == 0 ? []
            : builder.DefineGenericParameters(Enumerable.Range(0, types.Length).Select(i => "T" + i).ToArray());
        Type self = types.Length == 0 ? builder : builder.MakeGenericType(parameters);
        builder.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes)!, []));
        var fields = new FieldBuilder[types.Length];
        var comparers = new FieldBuilder[types.Length];
        var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, parameters);
        ILGenerator init = ctor.GetILGenerator();
        init.Emit(OpCodes.Ldarg_0);
        init.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        for (int i = 0; i < types.Length; i++)
        {
            ctor.DefineParameter(i + 1, ParameterAttributes.None, names[i]);
            fields[i] = builder.DefineField("_" + i, parameters[i], FieldAttributes.Private | FieldAttributes.InitOnly);
            comparers[i] = builder.DefineField("_comparer" + i, typeof(IEqualityComparer), FieldAttributes.Private | FieldAttributes.Static);
            PropertyBuilder property = builder.DefineProperty(names[i], PropertyAttributes.None, parameters[i], null);
            MethodBuilder getter = builder.DefineMethod("get_" + names[i],
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, parameters[i], Type.EmptyTypes);
            ILGenerator read = getter.GetILGenerator();
            read.Emit(OpCodes.Ldarg_0);
            read.Emit(OpCodes.Ldfld, fields[i]);
            read.Emit(OpCodes.Ret);
            property.SetGetMethod(getter);
            init.Emit(OpCodes.Ldarg_0);
            init.Emit(OpCodes.Ldarg, (short)(i + 1));
            init.Emit(OpCodes.Stfld, fields[i]);
        }

        init.Emit(OpCodes.Ret);
        MethodAttributes flags = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig;
        ILGenerator equal = builder.DefineMethod(nameof(Equals), flags, typeof(bool), [typeof(object)]).GetILGenerator();
        LocalBuilder other = equal.DeclareLocal(self);
        Label unequal = equal.DefineLabel();
        equal.Emit(OpCodes.Ldarg_1);
        equal.Emit(OpCodes.Isinst, self);
        equal.Emit(OpCodes.Stloc, other);
        equal.Emit(OpCodes.Ldloc, other);
        equal.Emit(OpCodes.Brfalse, unequal);
        ILGenerator hash = builder.DefineMethod(nameof(GetHashCode), flags, typeof(int), Type.EmptyTypes).GetILGenerator();
        hash.Emit(OpCodes.Ldc_I4_0);
        for (int i = 0; i < types.Length; i++)
        {
            // An interface call avoids a dynamic-assembly visibility violation for a private
            // registered component. The factory installs exactly EqualityComparer<T>.Default.
            equal.Emit(OpCodes.Ldsfld, comparers[i]);
            equal.Emit(OpCodes.Ldarg_0);
            equal.Emit(OpCodes.Ldfld, fields[i]);
            equal.Emit(OpCodes.Box, parameters[i]);

            equal.Emit(OpCodes.Ldloc, other);
            equal.Emit(OpCodes.Ldfld, fields[i]);
            equal.Emit(OpCodes.Box, parameters[i]);

            equal.Emit(OpCodes.Callvirt, typeof(IEqualityComparer).GetMethod("Equals", [typeof(object), typeof(object)])!);
            equal.Emit(OpCodes.Brfalse, unequal);
            hash.Emit(OpCodes.Ldc_I4, -1521134295);
            hash.Emit(OpCodes.Mul);
            hash.Emit(OpCodes.Ldsfld, comparers[i]);
            hash.Emit(OpCodes.Ldarg_0);
            hash.Emit(OpCodes.Ldfld, fields[i]);
            hash.Emit(OpCodes.Box, parameters[i]);

            hash.Emit(OpCodes.Callvirt, typeof(IEqualityComparer).GetMethod("GetHashCode", [typeof(object)])!);
            hash.Emit(OpCodes.Add);
        }

        equal.Emit(OpCodes.Ldc_I4_1);
        equal.Emit(OpCodes.Ret);
        equal.MarkLabel(unequal);
        equal.Emit(OpCodes.Ldc_I4_0);
        equal.Emit(OpCodes.Ret);
        hash.Emit(OpCodes.Ret);
        ILGenerator format = builder.DefineMethod(nameof(ToString), flags, typeof(string), Type.EmptyTypes).GetILGenerator();
        format.Emit(OpCodes.Ldstr, names.Count == 0 ? "{{ }}"
            : "{{ " + string.Join(", ", names.Select((n, i) => n + " = {" + i + "}")) + " }}");
        format.Emit(OpCodes.Ldc_I4, types.Length);
        format.Emit(OpCodes.Newarr, typeof(object));
        for (int i = 0; i < types.Length; i++)
        {
            format.Emit(OpCodes.Dup);
            format.Emit(OpCodes.Ldc_I4, i);
            format.Emit(OpCodes.Ldarg_0);
            format.Emit(OpCodes.Ldfld, fields[i]);
            format.Emit(OpCodes.Box, parameters[i]);

            format.Emit(OpCodes.Stelem_Ref);
        }

        format.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Format), [typeof(string), typeof(object[])])!);
        format.Emit(OpCodes.Ret);
        Type definition = builder.CreateType()!;
        Type generated = types.Length == 0 ? definition : definition.MakeGenericType(types);
        for (int i = 0; i < types.Length; i++)
        {
            object comparer = typeof(EqualityComparer<>).MakeGenericType(types[i]).GetProperty("Default")!.GetValue(null)!;
            generated.GetField("_comparer" + i, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, comparer);
        }

        return generated;
    }
}
