// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     The F1 model on ADR-009 Tier A, shared by <see cref="SerializationInfoCarrierTest" /> and
///     <see cref="DataBindingInfoCarrierTest" /> exactly as EF shares its own <c>F1InMemoryFixture</c>.
/// </summary>
/// <remarks>
///     <para>
///         <c>F1FixtureBase</c> builds its model <em>externally</em> and applies it with
///         <c>UseModel</c>, deliberately — it is EF's regression coverage for doing so — and
///         <c>F1Context</c> has no <c>OnModelCreating</c> of its own. So the usual
///         <c>OnModelCreating</c> route this repo's stores take would leave the server with a bare
///         convention model. The server is handed its own copy instead, built over the *backing
///         store's* convention set rather than this provider's.
///     </para>
///     <para>
///         This is the Tier A twin of <c>OptimisticConcurrencyInfoCarrierTest.InfoCarrierFixture</c>,
///         and simpler for one reason: an InMemory server needs no table mappings, so
///         <c>BuildModelExternal</c> is the whole of it.
///     </para>
/// </remarks>
public class F1InfoCarrierFixture : F1FixtureBase<byte[]>
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    public override TestHelpers TestHelpers
        => InfoCarrierTestHelpers.Instance;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            onAddOptions: b => b
                .UseModel(BuildServerModel())
                .UseSeeding((c, _) =>
                {
                    if (((F1Context)c).EngineSuppliers.Any())
                    {
                        return;
                    }

                    F1Context.AddSeedData((F1Context)c);
                    c.SaveChanges();
                })
                .UseAsyncSeeding(async (c, _, t) =>
                {
                    if (await ((F1Context)c).EngineSuppliers.AnyAsync(t))
                    {
                        return;
                    }

                    F1Context.AddSeedData((F1Context)c);
                    await c.SaveChangesAsync(t);
                }),
            onAddServices: s => s.AddSingleton<ISingletonInterceptor, F1MaterializationInterceptor>(),
            configureConventions: ConfigureConventions);

    /// <inheritdoc />
    /// <remarks>
    ///     Mirrors <c>F1InMemoryFixtureBase</c>, which ignores InMemory's equivalent warning: this
    ///     provider ignores transactions on Tier A (roadmap M4).
    /// </remarks>
    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base.AddOptions(builder)
            .ConfigureWarnings(w => w.Ignore(InfoCarrierEventId.TransactionIgnoredWarning));

    /// <summary>
    ///     The same model as the client's, over the backing store's convention set.
    /// </summary>
    private IModel BuildServerModel()
    {
        ModelBuilder builder = InMemoryConventionSetBuilder.CreateModelBuilder();

        BuildModelExternal(builder);

        return (IModel)builder.Model;
    }
}

/// <summary>
///     <c>SerializationTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A tracked graph serialized with <c>System.Text.Json</c> and Newtonsoft, which is a pointed
///     thing to ask of this provider: the entities under test were built by
///     <c>ClientResultMaterializer</c> rather than by EF's shaper, and a navigation left pointing
///     at a half-built instance shows up here as a cycle rather than as a wrong answer.
/// </remarks>
public class SerializationInfoCarrierTest(F1InfoCarrierFixture fixture)
    : SerializationTestBase<F1InfoCarrierFixture>(fixture);

/// <summary>
///     <c>DataBindingTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     <c>ToObservableCollection</c> / <c>Local</c> over the change tracker: the binding lists are
///     built from tracked entries, so this is a check that what this provider puts in the tracker
///     behaves like what EF's shaper puts there.
/// </remarks>
public class DataBindingInfoCarrierTest(F1InfoCarrierFixture fixture)
    : DataBindingTestBase<F1InfoCarrierFixture>(fixture);
