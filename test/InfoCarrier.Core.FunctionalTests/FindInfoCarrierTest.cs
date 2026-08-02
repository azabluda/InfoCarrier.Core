// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

#nullable disable

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>FindTestBase</c> on ADR-009 Tier A, mirroring EF's own <c>FindInMemoryTest</c>.
/// </summary>
/// <remarks>
///     <c>Find</c> is worth its own coverage here because it is the one read that may not reach
///     the server at all: a tracked entity is answered from the client's change tracker, and only
///     a miss becomes a query. The three nested classes are EF's, one per way of reaching
///     <c>Find</c>.
/// </remarks>
public abstract class FindInfoCarrierTest(FindInfoCarrierTest.InfoCarrierFixture fixture)
    : FindTestBase<FindInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    public class FindInfoCarrierTestSet(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaSetFinder();
    }

    public class FindInfoCarrierTestContext(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaContextFinder();
    }

    public class FindInfoCarrierTestNonGeneric(InfoCarrierFixture fixture) : FindInfoCarrierTest(fixture)
    {
        protected override TestFinder Finder { get; } = new FindViaNonGenericContextFinder();
    }

    public class InfoCarrierFixture : FindFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "FindInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context));
    }
}
