// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Data.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.FunctionalTests.Sqlite;

/// <summary>
///     <see cref="CurrentTest" /> names the running test, row by row, and the server's statements
///     see the same name (#167, step H0).
/// </summary>
/// <remarks>
///     In the Sqlite namespace because the last pin needs a server that runs SQL.
/// </remarks>
public class CurrentTestTest(CurrentTestTest.Fixture fixture) : IClassFixture<CurrentTestTest.Fixture>
{





    /// <summary>A class fixture is built before any test starts, so a fixture's seeding is nobody's.</summary>
    [Fact]
    public void A_class_fixture_is_built_outside_any_test()
    {
        Assert.True(fixture.Constructed);
        Assert.Null(fixture.CurrentTestInConstructor);
    }


    /// <summary>The value reaches the server's command interceptor through the wire.</summary>
    [Fact]
    public async Task A_statement_the_server_runs_carries_the_display_name()
    {
        var names = new NameRecorder();
        await using var store = new SqliteInfoCarrierBackendTestStore(
            Guid.NewGuid().ToString(),
            shared: false,
            new SharedTestStoreProperties
            {
                ContextType = typeof(ServerSqlContext),
                OnModelCreating = (_, _) => { },
                OnAddOptions = b => b.AddInterceptors(names),
            });
        await store.InitializeAsync(store.ServiceProvider, store.CreateDbContext, seed: _ => Task.CompletedTask);
        await using var client = new ServerSqlContext(
            new DbContextOptionsBuilder<ServerSqlContext>().UseInfoCarrier(store).Options);

        names.Names.Clear();
        _ = await client.Tickets.ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [$"{typeof(CurrentTestTest).FullName}.{nameof(A_statement_the_server_runs_carries_the_display_name)}"],
            names.Names);
    }


    /// <summary>Records what <see cref="CurrentTest" /> said while it was built.</summary>
    public sealed class Fixture
    {
        public Fixture()
        {
            Constructed = true;
            CurrentTestInConstructor = CurrentTest.Value;
        }

        public bool Constructed { get; }

        public CurrentTest? CurrentTestInConstructor { get; }
    }

    private sealed class NameRecorder : DbCommandInterceptor
    {
        public ConcurrentQueue<string?> Names { get; } = new();

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Names.Enqueue(CurrentTest.Value?.DisplayName);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Names.Enqueue(CurrentTest.Value?.DisplayName);
            return new(result);
        }
    }
}
