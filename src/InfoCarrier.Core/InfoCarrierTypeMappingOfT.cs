// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;

namespace InfoCarrier.Core;

/// <summary>
///     A client mapping whose compiled-model default retains its CLR type.
/// </summary>
/// <typeparam name="T">The mapped CLR type.</typeparam>
public sealed class InfoCarrierTypeMapping<T> : InfoCarrierTypeMapping
{
    /// <summary>
    ///     The default used by EF Core's compiled-model generator.
    /// </summary>
    public static InfoCarrierTypeMapping<T> Default { get; } = new();

    /// <summary>
    ///     Initializes a mapping with optional comparison and JSON behavior.
    /// </summary>
    /// <param name="comparer">The value comparer.</param>
    /// <param name="keyComparer">The key comparer.</param>
    /// <param name="jsonValueReaderWriter">The JSON reader and writer.</param>
    public InfoCarrierTypeMapping(
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null,
        JsonValueReaderWriter? jsonValueReaderWriter = null)
        : base(typeof(T), comparer, keyComparer, jsonValueReaderWriter)
    {
    }

    private InfoCarrierTypeMapping(RelationalTypeMappingParameters parameters)
        : base(parameters)
    {
    }

    /// <inheritdoc />
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
        => new InfoCarrierTypeMapping<T>(parameters);

}
