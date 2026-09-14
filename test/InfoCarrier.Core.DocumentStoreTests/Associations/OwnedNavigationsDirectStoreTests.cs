// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.DocumentStoreTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query.Associations.OwnedNavigations;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.DocumentStoreTests.Associations;

/// <summary>
///     EF's whole <c>OwnedNavigations</c> family against MongoDB with InfoCarrier removed. The
///     control for Tier D, and the citation behind every override in it.
/// </summary>
/// <remarks>
///     <para>
///         <b>A RED ON TIER D IS NOT EVIDENCE UNTIL SOMEBODY ASKS WHETHER THE STORE CAN ANSWER THE
///         QUERY AT ALL.</b> Six classes here run the identical bases on plain EF Core over the
///         same embedded MongoDB. Red here and red on Tier D is the store's; red on Tier D alone is
///         ours. It replaced thirty-one hand-written probe queries with seventy lines, and it
///         answers for every test rather than the ones somebody thought to ask about.
///     </para>
///     <para>
///         <b>WHY THE SUITE CARRIES THE CONTROL'S OWN REDS RATHER THAN HIDING THEM.</b> These
///         classes fail too, and their failures sit in <c>test/known-failures.txt</c> beside Tier
///         D's. They are labelled <c>Direct*</c> so a reader can see at a glance which half of the
///         list is the store's own behaviour and which is this provider's. A control whose result
///         is not visible is an assertion, not a measurement.
///     </para>
///     <para>
///         <b>AND THE CONTROL IS GATED, BECAUSE IT IS A CONFLICT OF INTEREST.</b> The worse it is
///         wired the more it fails, and the more of Tier D's reds are attributed to the store
///         rather than to us — a bias that needs no bad intent to operate, and whose failure mode
///         is silent. <c>eng/tier-d-control.py</c> fails the build if this control fails anything
///         Tier D passes, outside the named allowances in
///         <c>test/tier-d-control-allowances.txt</c>. That inverts the incentive: sloppiness here
///         breaks CI instead of making this provider look clean.
///     </para>
///     <para>
///         <b>The first version of this control had exactly that bug</b>, and it is recorded
///         because it is the shape of the risk rather than a one-off. It passed <c>null</c> for the
///         model customization, so <see cref="MongoInfoCarrierBackendTestStore" />'s own server
///         context would have been built from a DIFFERENT model than Tier D measures. Two classes
///         passing completely — <c>Miscellaneous</c> and <c>PrimitiveCollection</c> — were what
///         showed the control was substantially sound; the gate is what makes that a check rather
///         than a hope.
///     </para>
/// </remarks>
public class OwnedNavigationsDirectStoreFixture : OwnedNavigationsFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= new MongoDirectTestStoreFactory(
            (modelBuilder, context) => OnModelCreating(modelBuilder, context));

    /// <inheritdoc />
    /// <remarks>
    ///     A store of its own, because one server per test class is this tier's rule and this
    ///     fixture is shared by six classes.
    /// </remarks>
    protected override string StoreName => "OwnedNavigationsDirect";
}

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectMiscellaneousTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsMiscellaneousTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectPrimitiveCollectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsPrimitiveCollectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectProjectionTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsProjectionTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectSetOperationsTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsSetOperationsTestBase<OwnedNavigationsDirectStoreFixture>(fixture);

/// <inheritdoc cref="OwnedNavigationsDirectStoreFixture" />
public class DirectStructuralEqualityTest(OwnedNavigationsDirectStoreFixture fixture)
    : OwnedNavigationsStructuralEqualityTestBase<OwnedNavigationsDirectStoreFixture>(fixture);
