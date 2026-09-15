// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's <c>OwnedNavigations</c> model over ADR-009 Tier D, which is the first specification
///     base this tier adopts.
/// </summary>
/// <remarks>
///     <para>
///         <b>This family and not another, and the rule it came from is worth stating.</b> A base is
///         worth adopting here in proportion to how many of its tests actually RUN on the store, not
///         to whether Microsoft ships it. The official MongoDB provider adopts the whole Northwind
///         spine, but it adopts it to assert the MQL its translator emits, and those files are
///         mostly baseline text for tests the store refuses. This family is nested documents, which
///         is the one shape this tier exists to exercise, and a twenty-shape probe of EF's own model
///         against this store ran seventeen of them identically with and without the wire.
///     </para>
///     <para>
///         <b>The official provider does NOT adopt this family</b>; its <c>MongoComplianceTest</c>
///         ignores all six bases as "not yet overridden". So a red here may be theirs rather than
///         ours, and <c>ServerSideControlTest</c>'s fork is the way to tell: what fails identically
///         with and without the wire is not this repository's.
///     </para>
///     <para>
///         <b><c>RootReferencingEntity</c> IS MAPPED, AND IT WAS IGNORED UNTIL 2026-09-14 FOR A
///         REASON BORROWED FROM THE WRONG STORE.</b> This paragraph read <i>"ignored, exactly as
///         the Cosmos fixture ignores it"</i>, and that was the whole argument: EF's Cosmos suite
///         drops the type, so this one did too. <b>Cosmos is not MongoDB.</b> They are both
///         document stores and they are different products, with different providers written by
///         different teams, and nothing about a decision taken for one is evidence about the other.
///     </para>
///     <para>
///         <b>Measured instead, and the ignore was buying nothing.</b> With the type mapped the
///         failing test NAMES are byte-identical - same 38, none added, none removed. What changes
///         is what the two tests that need it REPORT: EF's real message about tracking an owned
///         entity without its owner, and a real <c>NullReferenceException</c>, in place of
///         <c>Cannot create a DbSet for 'RootReferencingEntity'</c>, which only ever described a
///         type this fixture had removed. A red that describes our own model teaches nothing.
///     </para>
///     <para>
///         <b>Nothing here names a MongoDB-only API, and that is a constraint of the harness rather
///         than a preference.</b> One model is built from one customization and given to BOTH
///         halves, and the client has no MongoDB provider — so <c>ToCollection</c>, which
///         <see cref="ShopServerContext" /> can say because it has a hand-written client twin,
///         cannot appear in a specification fixture. The collection name comes from the provider's
///         own convention instead.
///     </para>
/// </remarks>
public class OwnedNavigationsInfoCarrierFixture : OwnedNavigationsFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            MongoInfoCarrierTier.Instance,
            typeof(PoolableDbContext),
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            onAddServices: services => services.AddInfoCarrierServerDocumentStore());

    // NO OnModelCreating AND NO AddOptions OVERRIDE, AND BOTH EXISTED UNTIL 2026-09-14. One
    // ignored RootReferencingEntity and the other suppressed the MappedEntityTypeIgnoredWarning
    // that ignoring it raised -- a suppression that existed only to serve the ignore. Removing the
    // pair leaves the failing test names byte-identical, so the model this tier measures is now
    // EF's own, unedited, and there is nothing here to explain to the next reader.
}

/// <summary>
///     The same fixture under a different store name, for
///     <see cref="OwnedNavigationsServerSideControlTest" />.
/// </summary>
/// <remarks>
///     <b>ONE SERVER PER TEST CLASS IS THIS TIER'S RULE, AND A SHARED FIXTURE TYPE BREAKS IT.</b>
///     Two classes taking <c>IClassFixture&lt;T&gt;</c> of the same type get two INSTANCES, each
///     starting a <c>mongod</c> of its own — and with one store name between them the two raced on
///     which server the name belonged to, which arrived as
///     <c>MongoConnectionException: An exception occurred while receiving a message from the
///     server</c> in whichever class finished second. A distinct name gives each class a store of
///     its own, which is what <c>DocumentStoreFixture</c> already gives the hand-written classes.
/// </remarks>
public sealed class OwnedNavigationsControlFixture : OwnedNavigationsInfoCarrierFixture
{
    /// <inheritdoc />
    protected override string StoreName => "OwnedNavigationsControl";
}
