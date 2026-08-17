// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>CompositeKeyEndToEndTestBase</c> on Tier A: save and re-read entities keyed by more
///     than one property.
/// </summary>
/// <remarks>
///     Every key path in this provider is written for a composite key — the identity map lookups
///     in <c>ClientResultMaterializer</c>, the correlation of store-generated values, the
///     shared-identity pairing in <c>ServerSaveChangesExecutor</c> — but almost everything
///     exercising them so far has had a single-property key.
/// </remarks>
public class CompositeKeyEndToEndInfoCarrierTest(CompositeKeyEndToEndInfoCarrierTest.InfoCarrierFixture fixture)
    : CompositeKeyEndToEndTestBase<CompositeKeyEndToEndInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class InfoCarrierFixture : CompositeKeyEndToEndFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "CompositeKeyEndToEndInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
