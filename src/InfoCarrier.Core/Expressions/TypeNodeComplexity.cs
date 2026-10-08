// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Bounds descriptor work before cache-key traversal or runtime type lookup and construction.
///     This validates syntax and complexity only; permission still applies to the resolved whole type.
/// </summary>
internal static class TypeNodeComplexity
{
    private const int MaximumDepth = 16;
    private const int MaximumNodes = 1024;
    private const int MaximumNameLength = 16384;
    private static readonly TypeNameParseOptions NameParseOptions = new() { MaxNodes = MaximumNodes };

    internal static void Validate(TypeNode node)
    {
        int remainingNodes = MaximumNodes;
        Validate(node, depth: 0, ref remainingNodes);
    }

    private static void Validate(TypeNode node, int depth, ref int remainingNodes)
    {
        CheckDepth(depth);
        if (node is null || node.GenericArguments is null)
        {
            throw new InvalidOperationException("Invalid type descriptor: a node or its generic arguments are null.");
        }

        if (remainingNodes == 0)
        {
            throw new InvalidOperationException("The type descriptor node budget is exhausted.");
        }

        if (string.IsNullOrEmpty(node.Name))
        {
            throw new InvalidOperationException("Invalid CLR type name: a name is required.");
        }

        if (node.Name.Length > MaximumNameLength || node.EntityTypeName?.Length > MaximumNameLength)
        {
            throw new InvalidOperationException("The type descriptor name length budget is exhausted.");
        }

        TypeName name;
        try
        {
            // This parser creates metadata names, not runtime Types, and never loads an assembly.
            name = TypeName.Parse(node.Name, NameParseOptions);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException("Invalid or excessively complex CLR type name.", error);
        }

        remainingNodes -= name.GetNodeCount();
        if (remainingNodes < 0 || node.GenericArguments.Count > remainingNodes)
        {
            throw new InvalidOperationException("The type descriptor node budget is exhausted.");
        }

        ValidateNameDepth(name, depth);
        if (node.GenericArguments.Count > 0 && !name.IsSimple)
        {
            throw new InvalidOperationException("Structured generic arguments require a simple generic definition name.");
        }

        foreach (TypeNode argument in node.GenericArguments)
        {
            Validate(argument, depth + 1, ref remainingNodes);
        }
    }

    private static void ValidateNameDepth(TypeName name, int depth)
    {
        CheckDepth(depth);
        if (name.IsArray || name.IsPointer || name.IsByRef)
        {
            ValidateNameDepth(name.GetElementType(), depth + 1);
        }
        else if (name.IsConstructedGenericType)
        {
            foreach (TypeName argument in name.GetGenericArguments())
            {
                ValidateNameDepth(argument, depth + 1);
            }
        }
    }

    private static void CheckDepth(int depth)
    {
        if (depth > MaximumDepth)
        {
            throw new InvalidOperationException("The type descriptor depth budget is exhausted.");
        }
    }
}
