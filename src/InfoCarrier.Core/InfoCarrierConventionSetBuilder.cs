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
public class InfoCarrierConventionSetBuilder(
    ProviderConventionSetBuilderDependencies dependencies,
    Metadata.IInfoCarrierDocumentMapping documentMapping)
    : ProviderConventionSetBuilder(dependencies)
{
    /// <inheritdoc />
    public override ConventionSet CreateConventionSet()
    {
        ConventionSet conventionSet = base.CreateConventionSet();

        conventionSet.Replace<KeyDiscoveryConvention>(
            new InfoCarrierKeyDiscoveryConvention(Dependencies, documentMapping));

        // A property the caller gave a store default is store-generated, and only a relational
        // convention says so — which this provider does not run. Same reason as the key convention
        // above: where the answer is decided by the caller's own model configuration rather than by
        // the store, the client has to reach it too.
        conventionSet.ModelFinalizingConventions.Add(new InfoCarrierValueGenerationConvention());

        // And the same again for inheritance. Core EF gives every hierarchy a discriminator, and
        // the convention that takes it back for TPT and TPC is relational. Without this the client
        // keeps a discriminator the server's model has dropped.
        conventionSet.ModelFinalizingConventions.Add(new InfoCarrierHierarchyMappingConvention());

        // And once more for query filters. Core EF's rewriter turns the `DbSet` a `FromSql*` call
        // reads into an `IQueryable`, which is not what that call's first parameter is, so a filter
        // written over raw SQL fails while the CLIENT's model is built. Only a relational
        // convention knows about `FromSql`, and this provider does not run one.
        conventionSet.Replace<QueryFilterRewritingConvention>(
            new InfoCarrierQueryFilterRewritingConvention(Dependencies));

        return conventionSet;
    }
}
