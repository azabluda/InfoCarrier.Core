// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>StoreGeneratedFixupTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     Fixup between entities whose keys the store generates — the case where the client's key
///     is temporary and cannot travel (wire-protocol W2, plan S3c-9).
/// </remarks>
public class StoreGeneratedFixupInfoCarrierTest(StoreGeneratedFixupInfoCarrierTest.InfoCarrierFixture fixture)
    : StoreGeneratedFixupTestBase<StoreGeneratedFixupInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The backend is EF's InMemory provider, which does not enforce foreign keys — so this
    ///     answers exactly what <c>StoreGeneratedFixupInMemoryTest</c> answers. It is the *store*
    ///     being described, and on this tier the store is InMemory.
    /// </summary>
    protected override bool EnforcesFKs
        => false;

    public class InfoCarrierFixture : StoreGeneratedFixupFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "StoreGeneratedFixupInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));
    }
}
