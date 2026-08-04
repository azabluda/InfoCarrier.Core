// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Captures fixture state (context type, model customization, options) so the
///     parameterless <see cref="Microsoft.EntityFrameworkCore.TestUtilities.ITestStoreFactory" />
///     members can build correctly-configured client and server contexts (v1 pattern).
/// </summary>
public struct SharedTestStoreProperties
{
    /// <summary>
    ///     The client <see cref="DbContext" /> type, and the server's too unless
    ///     <see cref="ServerContextType" /> overrides it.
    /// </summary>
    public Type ContextType;

    /// <summary>
    ///     The server <see cref="DbContext" /> type, when it must differ from the client's.
    /// </summary>
    /// <remarks>
    ///     The two models are shared, but how the backing store <em>produces</em> rows is not
    ///     part of that contract — a defining query for a keyless entity type is the server's
    ///     business alone. <see langword="null" /> means "same as <see cref="ContextType" />".
    /// </remarks>
    public Type? ServerContextType;

    /// <summary>
    ///     The fixture's model customization.
    /// </summary>
    public Action<ModelBuilder, DbContext>? OnModelCreating;

    /// <summary>
    ///     The fixture's convention configuration — the other half of its model.
    /// </summary>
    /// <remarks>
    ///     <see cref="OnModelCreating" /> is not all a fixture says about its model.
    ///     <c>ConfigureConventions</c> is where a type-wide rule goes: every
    ///     <c>NorthwindQueryFixtureBase</c> routes its model customizer through it,
    ///     <c>LazyLoadProxyTestBase</c> declares five complex types there, and
    ///     <c>StoreGeneratedFixtureBase</c> registers three dozen value converters. The server
    ///     builds <em>the same model as the client</em> or the wire has nothing to agree on, so it
    ///     needs both halves — <c>TestModelSource.GetFactory</c> has taken this since EF wrote it,
    ///     and it was simply never passed.
    /// </remarks>
    public Action<ModelConfigurationBuilder>? ConfigureConventions;

    /// <summary>
    ///     Additional options configuration applied to the server context.
    /// </summary>
    public Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? OnAddOptions;

    /// <summary>
    ///     Copies per-request parameters from the client context to the server context
    ///     (e.g. tenant prefix), invoked server-side per request.
    /// </summary>
    public Action<DbContext, DbContext>? CopyDbContextParameters;

    /// <summary>
    ///     Extra services the <em>server</em> provider needs.
    /// </summary>
    /// <remarks>
    ///     A fixture's <c>AddServices</c> configures the client provider, which is usually all
    ///     that matters. It is not enough when the fixture's own seed depends on those services:
    ///     <c>PropertyValuesFixtureBase</c> registers a materialization interceptor and then
    ///     asserts in <c>SeedAsync</c> that it ran, and the seed executes against the server.
    /// </remarks>
    public Func<IServiceCollection, IServiceCollection>? OnAddServices;
}
