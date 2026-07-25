// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace InfoCarrier.Core;

/// <summary>
///     Client-side value generator selector. Store-generated values are produced on the server
///     and flow back (requirements §2.2); the client uses the default value generators.
/// </summary>
public class InfoCarrierValueGeneratorSelector : ValueGeneratorSelector
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InfoCarrierValueGeneratorSelector" /> class.
    /// </summary>
    public InfoCarrierValueGeneratorSelector(ValueGeneratorSelectorDependencies dependencies)
        : base(dependencies)
    {
    }
}
