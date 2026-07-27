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
    ///     The <see cref="DbContext" /> type used on both client and server.
    /// </summary>
    public Type ContextType;

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
