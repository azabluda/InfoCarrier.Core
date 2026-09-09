// Licensed under the MIT license. See license.txt file in the project root for license information.

// The document-mapping seam is [Obsolete] as of 10.1.0 and is still registered by default, so
// this provider goes on using it until the major that removes it. Suppressed per FILE, which is
// how EF1001 is handled here and for the same reason: a NEW use elsewhere still warns.
// See docs/versioning.md, "Where a breaking change goes".
#pragma warning disable CS0618 // Type or member is obsolete.

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

        // INHERITANCE IS THE RELATIONAL PACKAGE'S NOW (#97 level 2, R128). Core EF gives every
        // hierarchy a discriminator and the convention that takes it back for TPT and TPC is
        // relational, so this class used to carry a narrower hand-written copy of EF's. It is gone:
        // `InfoCarrierRelationalConventionSetBuilder` extends this builder and adds EF's own.
        //
        // THERE IS NOTHING FOR A CLIENT TO REGISTER (corrected 2026-09-09). This said a client
        // whose backing store is relational "must therefore register
        // `AddInfoCarrierRelationalClient()`". R135 deleted that method and the opt-in with it:
        // `AddEntityFrameworkInfoCarrier` puts `InfoCarrierRelationalConventionSetBuilder` in the
        // `IProviderConventionSetBuilder` slot for every client, so the subclass below always runs
        // and this base is never the one EF resolves. A client over a NON-relational store gets the
        // same conventions and agrees with its server anyway, because the four the subclass adds
        // read the caller's own code rather than deciding anything a store decides.

        // And once more for query filters. Core EF's rewriter turns the `DbSet` a `FromSql*` call
        // reads into an `IQueryable`, which is not what that call's first parameter is, so a filter
        // written over raw SQL fails while the CLIENT's model is built. Only a relational
        // convention knows about `FromSql`, and this provider does not run one.
        conventionSet.Replace<QueryFilterRewritingConvention>(
            new InfoCarrierQueryFilterRewritingConvention(Dependencies));

        return conventionSet;
    }
}
