// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The test application's finite catalog input, collected from its trusted compiled methods.
///     No query is executed and no transport descriptor is inspected or registered. Generic test
///     bases and compiler closure classes are followed with their actual closed type arguments.
/// </summary>
public static class TrustedAnonymousShapeManifest
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(c => c.Value);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Assembly, Lazy<Type[]>> Manifests = new();

    /// <summary>Reads the trusted test assembly and its specification-test base classes.</summary>
    public static IReadOnlyList<Type> ForAssembly(Assembly assembly)
        => Manifests.GetOrAdd(assembly, a => new Lazy<Type[]>(() => Read(a))).Value;

    private static Type[] Read(Assembly assembly)
    {
        var shapes = new HashSet<Type>();
        var types = new HashSet<Type>();
        var methods = new HashSet<MethodBase>();
        var pending = new Queue<MethodBase>();
        foreach (Type type in assembly.GetTypes().Where(t => !t.ContainsGenericParameters))
        {
            VisitType(type);
        }

        while (pending.TryDequeue(out MethodBase? method))
        {
            byte[]? body = method.GetMethodBody()?.GetILAsByteArray();
            if (body is null)
            {
                continue;
            }

            Type[]? typeArguments = method.DeclaringType?.GetGenericArguments();
            Type[]? methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
            for (int offset = 0; offset < body.Length;)
            {
                byte first = body[offset++];
                short code = first == 0xfe ? (short)(0xfe00 | body[offset++]) : first;
                OperandType operand = Codes[code].OperandType;
                if (operand is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
                {
                    MemberInfo? member;
                    try
                    {
                        member = method.Module.ResolveMember(BitConverter.ToInt32(body, offset), typeArguments, methodArguments);
                    }
                    catch (FileNotFoundException)
                    {
                        // Specification assemblies also contain compiler/analyzer tests. Their
                        // build-only dependencies cannot supply any runtime query shape here.
                        member = null;
                    }

                    if (member is null)
                    {
                        offset += 4;
                        continue;
                    }
                    VisitType(member.DeclaringType);
                    switch (member)
                    {
                        case Type referenced:
                            VisitType(referenced);
                            break;
                        case FieldInfo field:
                            VisitType(field.FieldType);
                            break;
                        case MethodBase called:
                            VisitMethod(called);
                            break;
                    }
                }

                offset += operand switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(body, offset),
                    _ => 4,
                };
            }
        }

        return shapes.OrderBy(t => t.AssemblyQualifiedName, StringComparer.Ordinal).ToArray();

        bool Trusted(Assembly candidate)
            => candidate == assembly || candidate == typeof(TrustedAnonymousShapeManifest).Assembly
                || candidate.GetName().Name is "Microsoft.EntityFrameworkCore.Specification.Tests"
                    or "Microsoft.EntityFrameworkCore.Relational.Specification.Tests";

        void VisitType(Type? type)
        {
            if (type is null || type.ContainsGenericParameters || !types.Add(type))
            {
                return;
            }

            if (type.HasElementType)
            {
                VisitType(type.GetElementType());
            }

            foreach (Type argument in type.GetGenericArguments())
            {
                VisitType(argument);
            }

            if (!Trusted(type.Assembly))
            {
                return;
            }

            if (type.IsSealed && !type.IsVisible && type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal)
                && type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            {
                shapes.Add(type);
                foreach (PropertyInfo property in type.GetProperties())
                {
                    VisitType(property.PropertyType);
                }
            }

            VisitType(type.BaseType);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (MethodBase member in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
            {
                VisitMethod(member);
            }
        }

        void VisitMethod(MethodBase method)
        {
            if (method.ContainsGenericParameters || !Trusted(method.Module.Assembly) || !methods.Add(method))
            {
                return;
            }

            pending.Enqueue(method);
            if (method is MethodInfo info)
            {
                VisitType(info.ReturnType);
                foreach (Type argument in info.GetGenericArguments())
                {
                    VisitType(argument);
                }
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                VisitType(parameter.ParameterType);
            }
        }
    }
}
