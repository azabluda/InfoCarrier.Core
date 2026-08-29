// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Scaffolding;

/// <summary>
///     <c>CompiledModelTestBase</c>, on ADR-009 <b>Tier A</b> (C0).
/// </summary>
/// <remarks>
///     <para>
///         C0 priced this as expensive on the strength of the 112 generated baseline files EF ships
///         beside its own InMemory variant. <b>That price was wrong</b>: <c>AssertBaseline</c>
///         returns early when the baseline directory does not exist — <i>"cannot look for the
///         baseline"</i> — so the baselines are opt-in, and the base's real contract is one abstract
///         member, <c>TestHelpers</c>, which this project already has for <c>F1FixtureBase</c>'s
///         sake.
///     </para>
///     <para>
///         What is under test is that this provider's model can be <em>scaffolded into source,
///         compiled, and loaded back</em> as a runtime model matching the design-time one — which
///         for a provider whose whole job is shipping models across a wire is a more pertinent
///         question than the file count suggested.
///     </para>
/// </remarks>
public class CompiledModelInfoCarrierTest(NonSharedFixture fixture) : CompiledModelTestBase(fixture)
{
    private readonly NonSharedModelInfoCarrierHarness _harness = new(InfoCarrierTestStoreFactory.InMemory);

    private Action<ModelBuilder>? _lastOnModelCreating;

    /// <inheritdoc />
    protected override TestHelpers TestHelpers
        => InfoCarrierTestHelpers.Instance;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _harness.TestStoreFactory;

    /// <inheritdoc />
    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<IServiceCollection, IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true)
    {
        Fixture = null;

        // `Test` builds the context factory TWICE, and the second call is where every model in
        // this class would otherwise be lost. The first carries the test's model customization;
        // the second carries only `onConfiguring: o => o.UseModel(compiledModel)`, because the
        // client no longer needs `OnModelCreating` once it has the compiled model. The server
        // still does — it has no compiled model and builds its own from the same customization —
        // so the last one seen is carried forward. Without it the server's model is an empty
        // `DbContext` and every test says *"… is not in the server model"*.
        _lastOnModelCreating = onModelCreating ?? _lastOnModelCreating;

        // `onConfiguring` is deliberately NOT forwarded, which is the opposite of the A49
        // default and for a reason specific to this base: on the second call it says
        // `UseModel(compiledModel)`, and that model was generated *for this provider* — it
        // carries `InfoCarrierTypeMapping` on every property. Handing it to the InMemory server
        // would replace the model the server is meant to build for itself with the client's.
        _harness.Prepare(
            typeof(TContext), _lastOnModelCreating, addServices, onConfiguring: null, configureConventions,
            AddOptions);

        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider);
    }

    /// <summary>
    ///     Adds the product assembly to the references the scaffolded model is compiled against.
    /// </summary>
    /// <remarks>
    ///     The base adds EF's assemblies and this test assembly, and every provider adds its own
    ///     — <c>CompiledModelInMemoryTest</c> adds <c>Microsoft.EntityFrameworkCore.InMemory</c>
    ///     here for the same reason. The generated code names
    ///     <c>InfoCarrierTypeMapping.Default</c>.
    ///     <para>
    ///         The diagnostic this fixes is <b>CS0103</b> and not the CS0246 a missing reference
    ///         usually gives, which is worth knowing before diagnosing it again: the generated
    ///         <c>using InfoCarrier.Core;</c> resolves either way, because this test assembly is
    ///         <c>InfoCarrier.Core.FunctionalTests</c> and so contributes that namespace itself.
    ///         The using compiles; only the type is missing.
    ///     </para>
    /// </remarks>
    /// <param name="build">The source build to add references to.</param>
    /// <param name="filePath">Supplied by the compiler; the base uses it to locate baselines.</param>
    /// <returns>The same <see cref="BuildSource" />, so calls chain.</returns>
    protected override BuildSource AddReferences(BuildSource build, [CallerFilePath] string filePath = "")
    {
        base.AddReferences(build);
        build.References.Add(BuildReference.ByName("InfoCarrier.Core"));
        return build;
    }
}
