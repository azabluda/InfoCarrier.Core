// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace InfoCarrier.Core;

/// <summary>
///     Extension methods for configuring an InfoCarrier client context.
/// </summary>
public static class InfoCarrierDbContextOptionsBuilderExtensions
{
    /// <summary>
    ///     Configures the context to use the InfoCarrier provider, remoting all database
    ///     operations through the supplied <see cref="IInfoCarrierClient" />.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    /// <param name="client">The client that ships operations to the server.</param>
    /// <param name="infoCarrierOptionsAction">
    ///     Configures this provider's own options — currently
    ///     <see cref="InfoCarrierDbContextOptionsBuilder.AllowTypes" /> and nothing else. The
    ///     shape every EF Core provider uses, so a second option needs no new overload here.
    /// </param>
    /// <returns>The same builder for chaining.</returns>
    [RequiresUnreferencedCode(
         "InfoCarrier resolves types by the name carried on the wire, so the trimmer cannot know "
         + "which members a model needs and cannot be told with [DynamicallyAccessedMembers]. A trimmed "
         + "client does run; test the paths your model actually uses. See "
         + "https://azabluda.github.io/InfoCarrier.Core/platforms/blazor-webassembly/#trimming"),
     RequiresDynamicCode(
         "InfoCarrier builds and compiles expression trees at run time from the payload, and closes "
         + "generic types over types the payload names, neither of which Native AOT can generate.")]
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        IInfoCarrierClient client,
        Action<InfoCarrierDbContextOptionsBuilder>? infoCarrierOptionsAction = null)
    {
        InfoCarrierOptionsExtension extension = GetOrCreateExtension(optionsBuilder)
            .WithInfoCarrierClient(client);

        // `EnsureCoreOptionsFirst` **before** `AddOrUpdateExtension`, which is the order every EF
        // provider uses and is not arbitrary. `DbContextOptions.Extensions` yields extensions by
        // *insertion ordinal*, and that order is what `BuildOptionsFragment` prints in the
        // context-initialized log line. Configuring warnings is what first creates
        // `CoreOptionsExtension`, so doing it first puts the core options ahead of the provider's
        // — `"NoTracking using InfoCarrier"`, which is the shape `LoggingTestBase` composes its
        // expectation in (`ExpectedMessage("NoTracking " + DefaultOptions)`) and the shape every
        // other provider produces. Adding ours first printed `"using InfoCarrier NoTracking"`.
        EnsureCoreOptionsFirst(optionsBuilder);
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);

        // After the extension is in, because `InfoCarrierDbContextOptionsBuilder` reads the
        // current one back off the builder and adds to it. Running it first would have it clone an
        // extension this method then overwrites.
        infoCarrierOptionsAction?.Invoke(new InfoCarrierDbContextOptionsBuilder(optionsBuilder));

        return optionsBuilder;
    }

    /// <summary>
    ///     Configures the context to use the InfoCarrier provider (generic overload).
    /// </summary>
    [RequiresUnreferencedCode(
         "InfoCarrier resolves types by the name carried on the wire, so the trimmer cannot know "
         + "which members a model needs and cannot be told with [DynamicallyAccessedMembers]. A trimmed "
         + "client does run; test the paths your model actually uses. See "
         + "https://azabluda.github.io/InfoCarrier.Core/platforms/blazor-webassembly/#trimming"),
     RequiresDynamicCode(
         "InfoCarrier builds and compiles expression trees at run time from the payload, and closes "
         + "generic types over types the payload names, neither of which Native AOT can generate.")]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IInfoCarrierClient client,
        Action<InfoCarrierDbContextOptionsBuilder>? infoCarrierOptionsAction = null)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseInfoCarrier(
            (DbContextOptionsBuilder)optionsBuilder, client, infoCarrierOptionsAction);

    /// <summary>
    ///     Adds <see cref="CoreOptionsExtension" /> to the builder before this provider's own
    ///     extension goes in, so that EF's core options keep the lower insertion ordinal.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The ordering is the entire purpose and it is observable.</b>
    ///         <c>DbContextOptions.Extensions</c> yields extensions by insertion ordinal, and that
    ///         order is what the context-initialized log line prints. Core options first produces
    ///         <c>"NoTracking using InfoCarrier"</c>, which is the shape <c>LoggingTestBase</c>
    ///         composes its expectation in and the shape every other provider produces. Adding
    ///         this provider's extension first printed <c>"using InfoCarrier NoTracking"</c>.
    ///     </para>
    /// </remarks>
    private static void EnsureCoreOptionsFirst(DbContextOptionsBuilder optionsBuilder)
    {
        CoreOptionsExtension coreOptionsExtension =
            optionsBuilder.Options.FindExtension<CoreOptionsExtension>() ?? new CoreOptionsExtension();

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(coreOptionsExtension);
    }

    private static InfoCarrierOptionsExtension GetOrCreateExtension(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.Options.FindExtension<InfoCarrierOptionsExtension>()
            ?? new InfoCarrierOptionsExtension();
    /// <summary>
    ///     Configures the context to query through the given InfoCarrier client.
    /// </summary>
    /// <remarks>
    ///     <b>A binary-compatibility overload, kept because 10.0.0 shipped this signature.</b>
    ///     Adding an optional parameter is source-compatible and <em>binary</em> breaking: the
    ///     compiler emits one member and the old arity disappears from the assembly, which
    ///     <c>dotnet pack</c>'s package validation reports as <c>CP0002</c>.
    ///     <c>Directory.Build.props</c> states the promise this keeps, and the <c>Packages</c>
    ///     workflow is the only job that checks it — it runs on <c>main</c> alone, which is how
    ///     six of these reached <c>main</c> unnoticed. Delete when the baseline moves past 10.0.x.
    /// </remarks>
    [RequiresUnreferencedCode(
         "InfoCarrier resolves types by the name carried on the wire, so the trimmer cannot know "
         + "which members a model needs and cannot be told with [DynamicallyAccessedMembers]. A trimmed "
         + "client does run; test the paths your model actually uses. See "
         + "https://azabluda.github.io/InfoCarrier.Core/platforms/blazor-webassembly/#trimming"),
     RequiresDynamicCode(
         "InfoCarrier builds and compiles expression trees at run time from the payload, and closes "
         + "generic types over types the payload names, neither of which Native AOT can generate.")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static DbContextOptionsBuilder UseInfoCarrier(
        this DbContextOptionsBuilder optionsBuilder,
        IInfoCarrierClient client)
        => UseInfoCarrier(optionsBuilder, client, infoCarrierOptionsAction: null);

    /// <summary>
    ///     Configures the typed context to query through the given InfoCarrier client.
    /// </summary>
    /// <remarks>
    ///     See the overload above: a binary-compatibility shim for the 10.0.0 signature.
    /// </remarks>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="optionsBuilder">The builder being configured.</param>
    /// <param name="client">The client this context queries through.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    [RequiresUnreferencedCode(
         "InfoCarrier resolves types by the name carried on the wire, so the trimmer cannot know "
         + "which members a model needs and cannot be told with [DynamicallyAccessedMembers]. A trimmed "
         + "client does run; test the paths your model actually uses. See "
         + "https://azabluda.github.io/InfoCarrier.Core/platforms/blazor-webassembly/#trimming"),
     RequiresDynamicCode(
         "InfoCarrier builds and compiles expression trees at run time from the payload, and closes "
         + "generic types over types the payload names, neither of which Native AOT can generate.")]
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static DbContextOptionsBuilder<TContext> UseInfoCarrier<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        IInfoCarrierClient client)
        where TContext : DbContext
        => UseInfoCarrier(optionsBuilder, client, infoCarrierOptionsAction: null);

}
