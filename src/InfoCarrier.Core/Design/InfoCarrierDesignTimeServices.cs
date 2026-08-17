// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

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
///         <b>Core's annotation code generator, not the relational one.</b> This provider
///         references <c>Microsoft.EntityFrameworkCore.Relational</c> — for the metadata
///         vocabulary a backing store's model uses — but the client is never a relational context
///         (ADR-013), so its model carries no relational annotations to generate.
///         <c>TryAddCoreServices</c> supplies <c>CSharpRuntimeAnnotationCodeGenerator</c> and that
///         is the right one. Nothing provider-specific is overridden because this provider adds no
///         runtime annotation of its own; if it ever does, that override goes here.
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
    }
}
