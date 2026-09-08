// Licensed under the MIT license. See license.txt file in the project root for license information.

// EF's design-time code generators live in a `Design.Internal` namespace, and a provider that
// wants the relational one has to name it. EF Core's own providers suppress EF1001 per file
// for exactly this registration; see CLAUDE.md.
#pragma warning disable EF1001 // Internal EF Core API usage.

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: DesignTimeProviderServices("InfoCarrier.Core.Design.InfoCarrierDesignTimeServices")]

namespace InfoCarrier.Core.Design;

/// <summary>
///     The design-time services of the InfoCarrier client provider, named by the assembly-level
///     <see cref="DesignTimeProviderServicesAttribute" /> above.
/// </summary>
/// <remarks>
///     <para>
///         <b>This is about compiled models, not about schema</b> — <c>dotnet ef dbcontext
///         optimize</c>, which scaffolds a model into source so it need not be built at startup.
///         A client that has no database must never migrate and must never scaffold *from* one,
///         and both stay unavailable for the ordinary reason: nothing schema-related is
///         registered here, so <c>IMigrationsScaffolder</c> and <c>IDatabaseModelFactory</c> have
///         no provider implementation to resolve. Refusing them is not this type's job; not
///         offering them is.
///     </para>
///     <para>
///         <b>No third package reference, and the standing note that said otherwise was wrong.</b>
///         C8 recorded this work as needing <c>Microsoft.EntityFrameworkCore.Design</c> on the
///         product assembly. It does not: <see cref="IDesignTimeServices" />,
///         <see cref="DesignTimeProviderServicesAttribute" /> and
///         <see cref="EntityFrameworkDesignServicesBuilder" /> all live in
///         <c>Microsoft.EntityFrameworkCore</c> itself, which this project already references.
///         The <c>Design</c> package is what the *tool* loads, and it discovers this type through
///         the attribute rather than the other way round.
///     </para>
///     <para>
///         <b>EF's relational annotation code generator, and this paragraph said the opposite
///         until 2026-09-09.</b> It read <em>core's generator, not the relational one</em>, on the
///         reasoning that the client is never a relational context (ADR-013) so its model carries
///         no relational annotations. The client model has carried them since it began building a
///         relational model, and the registration below has been the relational one since. Nothing
///         provider-specific is overridden beyond that, because this provider adds no runtime
///         annotation of its own; if it ever does, that override goes here.
///     </para>
/// </remarks>
public class InfoCarrierDesignTimeServices : IDesignTimeServices
{
    /// <inheritdoc />
    public virtual void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);

        serviceCollection.AddEntityFrameworkInfoCarrier();

        new EntityFrameworkDesignServicesBuilder(serviceCollection)
            .TryAddCoreServices();

        // THE RELATIONAL CODE GENERATOR, because the client model now carries relational runtime
        // annotations. `RelationalModelRuntimeInitializer` puts a `RelationalModelDependencies` on
        // the model, and the CORE generator meets it as an unknown object and refuses: *"Cannot
        // scaffold C# literals of type 'RelationalModelDependencies'"*. EF's relational generator
        // strips exactly those annotations before generating, which is why every relational
        // provider registers it. Its dependency object is parameterless.
        serviceCollection.TryAddSingleton<RelationalCSharpRuntimeAnnotationCodeGeneratorDependencies>();
        serviceCollection.RemoveAll<ICSharpRuntimeAnnotationCodeGenerator>();
        serviceCollection.AddSingleton<ICSharpRuntimeAnnotationCodeGenerator, RelationalCSharpRuntimeAnnotationCodeGenerator>();
    }
}
