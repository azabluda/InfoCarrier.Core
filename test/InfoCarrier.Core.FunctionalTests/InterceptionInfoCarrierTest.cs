// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     <c>QueryExpressionInterceptionTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     An <c>IQueryExpressionInterceptor</c> sees the query tree on its way into compilation, which
///     for this provider is the tree that is about to be <em>captured and split</em> (ADR-006).
///     That makes the base a check on where the capture sits relative to EF's own interception
///     point, which nothing else here exercises.
///     <para>
///         The structure — an abstract half plus two concrete classes differing only in whether
///         they subscribe to the diagnostic listener — is EF's own
///         <c>QueryExpressionInterceptionInMemoryTestBase</c>, and so is the seed override and the
///         `Interceptor_does_not_leak_across_contexts` skip.
///     </para>
/// </remarks>
public abstract class QueryExpressionInterceptionInfoCarrierTestBase(
    QueryExpressionInterceptionInfoCarrierTestBase.InterceptionInfoCarrierFixtureBase fixture)
    : QueryExpressionInterceptionTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>EF's own InMemory suite does not run this either.</remarks>
    public override Task Interceptor_does_not_leak_across_contexts(bool async)
        => Task.CompletedTask;

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
        await base.SeedAsync(context);

        if (!context.Set<Singularity>().Any())
        {
            context.AddRange(
                new Singularity { Id = 77, Type = "Black Hole" },
                new Singularity { Id = 88, Type = "Bing Bang" },
                new Brane { Id = 77, Type = "Black Hole?" },
                new Brane { Id = 88, Type = "Bing Bang?" });

            await context.SaveChangesAsync();
        }

        context.ChangeTracker.Clear();

        return context;
    }

    public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "QueryExpressionInterceptionInfoCarrier";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        /// <remarks>
        ///     The base builds an internal service provider for the injected interceptors and it
        ///     must carry this provider's own services, exactly as EF's InMemory version adds
        ///     <c>AddEntityFrameworkInMemoryDatabase</c>.
        /// </remarks>
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(serviceCollection.AddEntityFrameworkInfoCarrier(), injectedInterceptors);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(c => c.Ignore(InfoCarrierEventId.TransactionIgnoredWarning));
    }
}

/// <inheritdoc cref="QueryExpressionInterceptionInfoCarrierTestBase" />
public class QueryExpressionInterceptionInfoCarrierTest(
    QueryExpressionInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : QueryExpressionInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<QueryExpressionInterceptionInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override bool ShouldSubscribeToDiagnosticListener
            => false;
    }
}

/// <inheritdoc cref="QueryExpressionInterceptionInfoCarrierTestBase" />
public class QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest(
    QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture fixture)
    : QueryExpressionInterceptionInfoCarrierTestBase(fixture),
        IClassFixture<QueryExpressionInterceptionWithDiagnosticsInfoCarrierTest.InterceptionInfoCarrierFixture>
{
    public class InterceptionInfoCarrierFixture : InterceptionInfoCarrierFixtureBase
    {
        protected override bool ShouldSubscribeToDiagnosticListener
            => true;
    }
}

/// <summary>
///     <c>SaveChangesInterceptionTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     <c>ISaveChangesInterceptor</c> runs on the <em>client</em> context, whose
///     <c>SaveChanges</c> is a wire call rather than a store write — so the base is a check that
///     remoting the save did not move it out from under EF's own interception points.
///     <c>SupportsOptimisticConcurrency</c> is <see langword="false" /> as in EF's InMemory
///     version, because the backing store performs no concurrency check.
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
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(serviceCollection.AddEntityFrameworkInfoCarrier(), injectedInterceptors);

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder)
                .ConfigureWarnings(c => c.Ignore(InfoCarrierEventId.TransactionIgnoredWarning));
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
