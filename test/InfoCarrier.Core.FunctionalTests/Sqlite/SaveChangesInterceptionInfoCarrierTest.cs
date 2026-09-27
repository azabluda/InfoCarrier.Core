// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <c>SaveChangesInterceptionTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         <c>ISaveChangesInterceptor</c> runs on the <em>client</em> context, whose
///         <c>SaveChanges</c> is a wire call rather than a store write — so the base is a check that
///         remoting the save did not move it out from under EF's own interception points.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Tier A followed EF's
///         InMemory class instead, where <c>SupportsOptimisticConcurrency</c> is
///         <see langword="false" /> because that store performs no concurrency check, and it
///         emptied the store before each context. EF's SQLite class does neither.
///     </para>
///     <para>
///         <b>Its first run here had 32 of its 112 tests red, in the concurrency tests Tier A never
///         ran.</b> Two causes were defects of this provider, fixed in <c>InfoCarrierDatabase</c>: a
///         synchronous save raised the interceptor's asynchronous concurrency hook, and the event
///         carried the server's exception rather than the one the save then throws. The third is
///         the limitation the override below asserts.
///     </para>
/// </remarks>
public abstract class SaveChangesInterceptionInfoCarrierTestBase(
    SaveChangesInterceptionInfoCarrierTestBase.InterceptionInfoCarrierFixtureBase fixture)
    : SaveChangesInterceptionTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         EF's own body, but for the count the save reports: 0 rather than 1. The server finds
    ///         the conflict, and its EF throws and rolls the save back before the client's
    ///         interceptor is asked, so there is nothing left for the suppression to complete. The
    ///         owner documented it as a limitation on 2026-09-27; a suppression registered on the
    ///         server behaves as it does with EF, which <c>ConcurrencyTokenTest</c> shows.
    ///     </para>
    ///     <para>
    ///         Everything else EF asserts holds: the client raises the event through the half the
    ///         save called, with the exception it would have thrown, and the save does not throw.
    ///     </para>
    /// </remarks>
    [InfoCarrierDesign(
        Decisions.Limitations, Decisions.ClientSuppressedConcurrency,
        Justification = "The server rolls the save back before a client-side interceptor can suppress the conflict.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Asserts that the save reports 0 entities where EF reports 1; the rest of the body is EF's, with Assert.Single for its count of the entries.")]
    public override async Task Intercept_to_suppress_concurrency_exception(bool async, bool inject, bool noAcceptChanges)
    {
        var (context, interceptor) = await CreateContextAsync<ConcurrencySuppressingSaveChangesInterceptor>(inject);

        using var _ = context;

        using var transaction = context.Database.BeginTransaction();

        var savingEventCalled = false;
        var resultFromEvent = -1;
        Exception? exceptionFromEvent = null;

        context.SavingChanges += (sender, args) =>
        {
            Assert.Same(context, sender);
            savingEventCalled = true;
        };

        context.SavedChanges += (sender, args) =>
        {
            Assert.Same(context, sender);
            resultFromEvent = args.EntitiesSavedCount;
        };

        context.SaveChangesFailed += (sender, args) =>
        {
            Assert.Same(context, sender);
            exceptionFromEvent = args.Exception;
        };

        var entry = context.Entry(new Singularity { Id = 35, Type = "Red Dwarf" });
        entry.State = EntityState.Modified;

        using var listener = Fixture.SubscribeToDiagnosticListener(context.ContextId);

        Exception? thrown = null;

        try
        {
            var __ = noAcceptChanges
                ? async
                    ? await context.SaveChangesAsync()
                    : context.SaveChanges()
                : async
                    ? await context.SaveChangesAsync(acceptAllChangesOnSuccess: false)
                    : context.SaveChanges(acceptAllChangesOnSuccess: false);
        }
        catch (Exception e)
        {
            thrown = e;
        }

        Assert.Equal(async, interceptor.AsyncCalled);
        Assert.NotEqual(async, interceptor.SyncCalled);
        Assert.NotEqual(interceptor.AsyncCalled, interceptor.SyncCalled);
        Assert.False(interceptor.FailedCalled);
        Assert.Same(context, interceptor.Context);
        Assert.Null(thrown);

        Assert.True(savingEventCalled);
        Assert.Equal(0, resultFromEvent);
        Assert.Null(exceptionFromEvent);

        Assert.True(interceptor.ConcurrencyExceptionCalled);
        Assert.Same(entry.Entity, Assert.Single(interceptor.Entries).Entity);

        listener.AssertEventsInOrder(
            CoreEventId.SaveChangesStarting.Name,
            CoreEventId.OptimisticConcurrencyException.Name,
            CoreEventId.SaveChangesCompleted.Name);
    }

    public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);

        /// <inheritdoc />
        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(TestStoreFactory.AddProviderServices(serviceCollection), injectedInterceptors);
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
        protected override string StoreName
            => "SaveChangesInterceptionInfoCarrier";

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
        protected override string StoreName
            => "SaveChangesInterceptionWithDiagnosticsInfoCarrier";

        protected override bool ShouldSubscribeToDiagnosticListener
            => true;
    }
}
