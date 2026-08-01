// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

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
    ///     Additional options configuration applied to the server context.
    /// </summary>
    public Func<DbContextOptionsBuilder, DbContextOptionsBuilder>? OnAddOptions;

    /// <summary>
    ///     Copies per-request parameters from the client context to the server context
    ///     (e.g. tenant prefix), invoked server-side per request.
    /// </summary>
    public Action<DbContext, DbContext>? CopyDbContextParameters;
}
