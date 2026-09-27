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
///         the limitation <c>website/docs/limitations.md</c> names; the override below runs the
///         configuration that page recommends.
///     </para>
/// </remarks>
public abstract class SaveChangesInterceptionInfoCarrierTestBase(
    SaveChangesInterceptionInfoCarrierTestBase.InterceptionInfoCarrierFixtureBase fixture)
    : SaveChangesInterceptionTestBase(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     <para>
    ///         <b>Runs the configuration the limitations page recommends</b>: the suppressing
    ///         interceptor on the server, where EF raises the event, and a passive one on the client.
    ///         The owner, 2026-09-27: "put the same interceptor on the server is the correct and
    ///         recommended approach", so this test asserts that configuration, and
    ///         <c>ConcurrencyTokenTest</c> pins what the other one does.
    ///     </para>
    ///     <para>
    ///         What the caller sees is EF's: no exception, one entity saved, no failure event. What
    ///         EF asserts about the interceptor instance is not asserted, because it describes one
    ///         <c>DbContext</c>: an interceptor on the server sees the server's context and the
    ///         server's entity objects, never the caller's (<c>Assert.Same(context,
    ///         interceptor.Context)</c> and <c>Assert.Same(entry.Entity, …)</c>). The entity it saw
    ///         is asserted by its key instead.
    ///     </para>
    /// </remarks>
    [InfoCarrierDesign(
        Decisions.Limitations, Decisions.ClientSuppressedConcurrency,
        Justification = "The interceptor that suppresses a concurrency exception belongs on the server, where EF raises it.",
        Deviation = DeviationKind.Other,
        DeviationNote = "The suppressing interceptor is registered on the server and a passive one on the client; the entity the server's interceptor saw is asserted by its key, not by instance.")]
    public override async Task Intercept_to_suppress_concurrency_exception(bool async, bool inject, bool noAcceptChanges)
    {
        ServerConcurrencySuppressor server = ((InterceptionInfoCarrierFixtureBase)Fixture).ServerSuppressor;
        server.Enabled = true;

        try
        {
            var (context, interceptor) = await CreateContextAsync<PassiveSaveChangesInterceptor>(inject);

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
            Assert.Equal(1, resultFromEvent);
            Assert.Null(exceptionFromEvent);

            Assert.Equal(35, Assert.IsType<Singularity>(Assert.Single(server.Entities)).Id);

            listener.AssertEventsInOrder(
                CoreEventId.SaveChangesStarting.Name,
                CoreEventId.SaveChangesCompleted.Name);
        }
        finally
        {
            server.Enabled = false;
        }
    }

    /// <summary>
    ///     The interceptor an application registers on the server to suppress a concurrency
    ///     exception, off until a test turns it on.
    /// </summary>
    /// <remarks>
    ///     One per fixture, and the tests of one class run one at a time. It passes every event
    ///     through while off, so the other tests see the server they always saw.
    /// </remarks>
    public sealed class ServerConcurrencySuppressor : SaveChangesInterceptor
    {
        private readonly List<object> _entities = [];
        private bool _enabled;

        /// <summary>Whether the next conflict is suppressed. Setting it forgets what was seen.</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                _entities.Clear();
            }
        }

        /// <summary>The entities of the conflicts suppressed since <see cref="Enabled" /> was set.</summary>
        public IReadOnlyList<object> Entities
            => _entities;

        /// <inheritdoc />
        public override InterceptionResult ThrowingConcurrencyException(
            ConcurrencyExceptionEventData eventData,
            InterceptionResult result)
            => Suppress(eventData, result);

        /// <inheritdoc />
        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(
            ConcurrencyExceptionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Suppress(eventData, result));

        private InterceptionResult Suppress(ConcurrencyExceptionEventData eventData, InterceptionResult result)
        {
            if (!_enabled)
            {
                return result;
            }

            _entities.AddRange(eventData.Entries.Select(e => e.Entity));
            return InterceptionResult.Suppress();
        }
    }

    public abstract class InterceptionInfoCarrierFixtureBase : InterceptionFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <summary>The server's suppressing interceptor, off unless a test turns it on.</summary>
        public ServerConcurrencySuppressor ServerSuppressor { get; } = new();

        /// <inheritdoc />
        /// <remarks>
        ///     The server's options carry <see cref="ServerSuppressor" />, as an application's server
        ///     carries its interceptors. The plain-EF half of #167's slow run takes the server's
        ///     options too, so there the one context has it.
        /// </remarks>
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: b => b.AddInterceptors(ServerSuppressor),
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
