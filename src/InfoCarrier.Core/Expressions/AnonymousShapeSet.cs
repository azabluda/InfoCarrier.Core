// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Immutable structural identities of anonymous types declared by trusted application code.
/// </summary>
internal sealed class AnonymousShapeSet
{
    private readonly IReadOnlySet<string> _identities;
    private readonly IReadOnlyList<Type> _types;

    private AnonymousShapeSet(IReadOnlySet<string> identities, IReadOnlyList<Type> types)
    {
        _identities = identities;
        _types = types;
    }

    internal static AnonymousShapeSet Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal), []);

    internal IReadOnlyList<Type> Types => _types;

    internal bool Contains(Type type)
        => AnonymousShapeTypes.IsOriginal(type)
            && _identities.Contains(Identity(type));

    internal AnonymousShapeSet Add(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var identities = new HashSet<string>(_identities, StringComparer.Ordinal);
        var registeredTypes = new List<Type>(_types);
        foreach (Type type in types)
        {
            Add(type, identities, registeredTypes);
        }

        return new AnonymousShapeSet(identities, registeredTypes);
    }

    internal static Type[] ValidatePrototypes(object[] shapes, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        Type[] types = shapes.Select(shape => shape?.GetType()
            ?? throw new ArgumentException("Anonymous-shape prototypes cannot be null.", parameterName)).ToArray();
        if (types.Any(type => !AnonymousShapeTypes.IsOriginal(type) || type.ContainsGenericParameters))
        {
            throw new ArgumentException(
                "Use anonymous objects as shape prototypes; register named records with AddInfoCarrierAllowedTypes.",
                parameterName);
        }

        return types;
    }

    private static void Add(Type type, ISet<string> identities, ICollection<Type> types)
    {
        if (!AnonymousShapeTypes.IsOriginal(type) || !identities.Add(Identity(type)))
        {
            return;
        }

        types.Add(type);
        foreach (Type component in type.GetGenericArguments())
        {
            Add(component, identities, types);
        }
    }

    private static string Identity(Type type)
        => AnonymousShapeTypes.StructuralIdentity(new TypeNodeMapper().ToTypeNode(type));
}
