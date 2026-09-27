// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>SaveChangesInterceptionTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     <para>
///         <c>ISaveChangesInterceptor</c> runs on the <em>client</em> context, whose
///         <c>SaveChanges</c> is a wire call rather than a store write — so the base is a check that
///         remoting the save did not move it out from under EF's own interception points.
///         <c>SupportsOptimisticConcurrency</c> is <see langword="false" /> as in EF's InMemory
///         version, because the backing store performs no concurrency check.
///     </para>
///     <para>
///         <b>It stays on Tier A, measured on 2026-09-27</b>, when the other Tier A bases that could
///         moved to Tier B for #167's slow run. On SQLite the base runs its concurrency tests, and 32
///         of the 112 tests of the two classes below were red, in two families.
///         <c>Intercept_to_suppress_concurrency_exception</c> expects a client's interceptor to see
///         <c>ThrowingConcurrencyException</c> and suppress it; EF raises that inside the update
///         pipeline, which here is the server's, so the client's interceptor is never asked.
///         <c>Intercept_SaveChanges_failed</c> with a concurrency error expects the interceptor to
///         see the very exception the caller catches; the caller catches one the client throws,
///         with the server's inside, and the interceptor sees another. Both are a question about what the
///         save protocol carries, not about the store. The <c>QueryExpressionInterception</c>
///         classes that shared this file moved.
///     </para>
/// </remarks>
public abstract class SaveChangesInterceptionInfoCarrierTestBase(
    SaveChangesInterceptionInfoCarrierTestBase.InterceptionInfoCarrierFixtureBase fixture)
    : SaveChangesInterceptionTestBase(fixture)
{
    /// <inheritdoc />
    protected override bool SupportsOptimisticConcurrency
        => false;

    /// <summary>
    ///     Empties the backing store before each context this base hands out.
    /// </summary>
    /// <remarks>
    ///     <c>InterceptionTestBase</c> seeds through <c>SeedAsync</c> on <em>every</em>
    ///     <c>CreateContextAsync</c>, and its tests then insert rows with fixed keys. That is sound
    ///     for every other provider because <c>Fixture.CreateOptions</c> builds a fresh internal
    ///     service provider per call, and an InMemory database is rooted in that provider — so each
    ///     test really does get an empty store. Here the client's provider is fresh but the
    ///     <em>server</em> is the fixture's one store, which persists, and the second test collides
    ///     with the first's rows ("An item with the same key has already been added. Key: 77").
    ///     Cleaning here restores the semantics the base is written against rather than changing
    ///     what it asserts.
    /// </remarks>
    public override async Task<UniverseContext> SeedAsync(UniverseContext context)
    {
        await Fixture.TestStore.CleanAsync(context);

        return await base.SeedAsync(context);
    }

    public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "SaveChangesInterceptionInfoCarrier";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InMemoryInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(serviceCollection.AddEntityFrameworkInfoCarrier(), injectedInterceptors);
    }
}

/// <inheritdoc cref="SaveChangesInterceptionInfoCarrierTestBase" />
public class SaveChangesInterceptionInfoCarrierTest(
    SaveChangesInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : SaveChangesInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<SaveChangesInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override bool ShouldSubscribeToDiagnosticListener
            => false;
    }
}

/// <inheritdoc cref="SaveChangesInterceptionInfoCarrierTestBase" />
public class SaveChangesInterceptionWithDiagnosticsInfoCarrierTest(
    SaveChangesInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : SaveChangesInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<SaveChangesInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override bool ShouldSubscribeToDiagnosticListener
            => true;
    }
}
