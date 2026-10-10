// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.Expressions;

/// <summary>
///     Immutable structural identities of anonymous types declared by trusted application code.
/// </summary>
internal sealed class AnonymousShapeSet
{
    private readonly IReadOnlySet<string> _identities;

    private AnonymousShapeSet(IReadOnlySet<string> identities)
        => _identities = identities;

    internal static AnonymousShapeSet Empty { get; } = new(new HashSet<string>(StringComparer.Ordinal));

    internal bool Contains(Type type)
        => AnonymousShapeTypes.IsOriginal(type)
            && _identities.Contains(Identity(type));

    internal AnonymousShapeSet Add(IEnumerable<Type> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var identities = new HashSet<string>(_identities, StringComparer.Ordinal);
        foreach (Type type in types)
        {
            Add(type, identities);
        }

        return new AnonymousShapeSet(identities);
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

    private static void Add(Type type, ISet<string> identities)
    {
        if (!AnonymousShapeTypes.IsOriginal(type) || !identities.Add(Identity(type)))
        {
            return;
        }

        foreach (Type component in type.GetGenericArguments())
        {
            Add(component, identities);
        }
    }

    private static string Identity(Type type)
        => AnonymousShapeTypes.StructuralIdentity(new TypeNodeMapper().ToTypeNode(type));
}
