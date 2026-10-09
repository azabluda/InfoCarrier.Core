// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     An immutable server catalog of closed anonymous projections approved by trusted application
///     configuration. Construction reserves and generates all replacements before requests use it.
///     Registration does not grant permission to deserialize the projections' component types.
/// </summary>
public sealed class AnonymousShapeCatalog
{
    internal static AnonymousShapeCatalog Empty { get; } = new(null, new Dictionary<string, Entry>());
    private readonly IReadOnlyDictionary<string, Entry> _entries;

    internal AnonymousShapeCatalog(IModel? model, IReadOnlyDictionary<string, Entry> entries)
    {
        Model = model;
        _entries = entries;
    }

    internal IModel? Model { get; }

    /// <summary>
    ///     Creates a complete catalog from trusted, closed compiler-generated anonymous types.
    ///     Nested anonymous components are included automatically. Construct the catalog before
    ///     accepting requests; never derive registrations from received payloads.
    /// </summary>
    /// <param name="model">The actual server model, or null for model-independent components.</param>
    /// <param name="shapes">Closed anonymous types from trusted application code.</param>
    /// <returns>The immutable, fully generated catalog.</returns>
    public static AnonymousShapeCatalog Create(IModel? model, IEnumerable<Type> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        return AnonymousShapeTypes.BuildCatalog(model, shapes);
    }

    internal Entry Require(TypeNode node)
    {
        if (!_entries.TryGetValue(AnonymousShapeTypes.StructuralIdentity(node), out Entry? entry))
        {
            // Server-only diagnostic marker. Preserve the exception type and wire fault contract.
            throw new InvalidOperationException("The anonymous shape is not registered in this server's trusted catalog.")
            {
                Data = { [typeof(AnonymousShapeCatalog)] = true },
            };
        }

        return entry;
    }

    internal static Type Resolve(Entry entry, Type[] components)
    {
        if (!entry.Components.SequenceEqual(components))
        {
            throw new InvalidOperationException("The anonymous-shape catalog's runtime component identities do not match this execution.");
        }

        return entry.Type;
    }

    internal sealed record Entry(Type Type, Type[] Components);
}
