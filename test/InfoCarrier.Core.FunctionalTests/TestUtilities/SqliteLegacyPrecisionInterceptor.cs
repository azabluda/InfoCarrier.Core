// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SQLitePCL;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

// EF Core 10.0.1's specification oracle converts double to decimal with 15 significant digits.
// SQLite's newer default preserves 17 when CAST(real AS TEXT) feeds ef_avg. Configure the store,
// rather than changing the inherited assertions or keeping a vulnerable native library.
// This is test-store configuration only; applications retain SQLite's native default.
internal sealed class SqliteLegacyPrecisionInterceptor : DbConnectionInterceptor
{
    public static readonly SqliteLegacyPrecisionInterceptor Instance = new();

    // SQLite 3.52+ documents this option, but SQLitePCLRaw 2.x does not declare its constant.
    // https://sqlite.org/c3ref/c_dbconfig_defensive.html#sqlitedbconfigfpdigits
    private const int FloatingPointDigits = 1023;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Configure((SqliteConnection)connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Configure((SqliteConnection)connection);
        return Task.CompletedTask;
    }

    internal static void Configure(SqliteConnection connection)
    {
        int result = raw.sqlite3_db_config(connection.Handle, FloatingPointDigits, 15, out int digits);
        if (result != raw.SQLITE_OK || digits != 15)
        {
            throw new InvalidOperationException(
                $"SQLite could not configure 15 significant digits (result {result}, digits {digits}).");
        }
    }
}
