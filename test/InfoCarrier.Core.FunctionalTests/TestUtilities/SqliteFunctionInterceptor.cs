// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     Defines a fixture's user-defined SQL functions on every SQLite connection the
///     <em>server</em> opens.
/// </summary>
/// <remarks>
///     <para>
///         EF's own SQL Server fixtures create their functions with <c>create function</c> in
///         <c>SeedAsync</c>, so the definitions live in the database and every connection sees
///         them. SQLite has no such statement: <see cref="SqliteConnection.CreateFunction{TResult}(string, Func{object?[], TResult}, bool)" />
///         attaches a delegate to <em>one open connection</em> and nothing is written to the
///         file. A function therefore has to be defined again each time a connection is opened,
///         which is what this interceptor is for.
///     </para>
///     <para>
///         It is registered through <c>SharedTestStoreProperties.OnAddOptions</c>, which
///         <see cref="InfoCarrierBackendTestStore.AddProviderOptions" /> applies to the server
///         context's options and nothing else. The client has no database and no connection to
///         intercept.
///     </para>
///     <para>
///         Defining a function twice on one connection replaces it, so re-registering on a
///         pooled connection that is opened again is harmless.
///     </para>
/// </remarks>
public sealed class SqliteFunctionInterceptor(Action<SqliteConnection> defineFunctions) : DbConnectionInterceptor
{
    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Define(connection);

    /// <inheritdoc />
    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Define(connection);
        return Task.CompletedTask;
    }

    private void Define(DbConnection connection)
    {
        if (connection is SqliteConnection sqliteConnection)
        {
            defineFunctions(sqliteConnection);
        }
    }
}
