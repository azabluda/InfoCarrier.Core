// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     The client's convention set: EF's core one, with key discovery replaced by
///     <see cref="InfoCarrierKeyDiscoveryConvention" />.
/// </summary>
/// <remarks>
///     One replacement, and the same one <c>RelationalConventionSetBuilder</c> makes. This
///     provider builds a model that has to <em>agree</em> with a model built by the backing
///     store's provider, so where a key shape is decided by the caller's own model configuration
///     rather than by the store, the client has to reach the same answer (B12, C80).
/// </remarks>
public class InfoCarrierConventionSetBuilder(ProviderConventionSetBuilderDependencies dependencies)
    : ProviderConventionSetBuilder(dependencies)
{
    /// <inheritdoc />
    public override ConventionSet CreateConventionSet()
    {
        ConventionSet conventionSet = base.CreateConventionSet();

        conventionSet.Replace<KeyDiscoveryConvention>(new InfoCarrierKeyDiscoveryConvention(Dependencies));

        return conventionSet;
    }
}
