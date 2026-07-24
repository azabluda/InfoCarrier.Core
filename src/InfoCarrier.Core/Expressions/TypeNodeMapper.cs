// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;
using InfoCarrier.Core.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Maps CLR <see cref="Type" />s to assembly-free <see cref="TypeNode" /> identities.
///     DI-resolved; consults the EF model (when available) to populate
///     <see cref="TypeNode.EntityTypeName" /> so entity types carry model identity
///     (research-findings §7).
/// </summary>
public class TypeNodeMapper
{
    private readonly IModel? _model;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TypeNodeMapper" /> class.
    /// </summary>
    /// <param name="model">The EF model used to tag entity types; null when no model is in scope
    ///     (e.g. pure client-side value mapping).</param>
    public TypeNodeMapper(IModel? model = null)
        => _model = model;

    /// <summary>
    ///     Maps a CLR type to its wire identity.
    /// </summary>
    public virtual TypeNode ToTypeNode(Type type)
    {
        TypeNode result = type.IsGenericType && !type.IsGenericTypeDefinition
            ? new TypeNode
            {
                Name = type.GetGenericTypeDefinition().FullName ?? type.Name,
                GenericArguments = type.GetGenericArguments().Select(ToTypeNode).ToList(),
                EntityTypeName = LookupEntityTypeName(type),
            }
            : new TypeNode
            {
                Name = type.FullName ?? type.Name,
                EntityTypeName = LookupEntityTypeName(type),
            };

        return result;
    }

    private string? LookupEntityTypeName(Type type)
        => _model?.FindEntityType(type)?.Name;
}
