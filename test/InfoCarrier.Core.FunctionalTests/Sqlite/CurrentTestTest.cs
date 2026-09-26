// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Data.Common;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

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
    private readonly string? _nameInConstructor = CurrentTest.Value?.DisplayName;

    [ConditionalFact]
    public void A_fact_sees_its_own_display_name()
        => Assert.Equal(
            $"{typeof(CurrentTestTest).FullName}.{nameof(A_fact_sees_its_own_display_name)}",
            CurrentTest.Value?.DisplayName);

    [ConditionalTheory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_serializable_row_sees_its_arguments(int row)
    {
        Assert.Equal(
            $"{typeof(CurrentTestTest).FullName}.{nameof(A_serializable_row_sees_its_arguments)}(row: {row})",
            CurrentTest.Value?.DisplayName);
        Assert.Equal(typeof(CurrentTestTest), CurrentTest.Value?.TestClass);
        Assert.Equal(nameof(A_serializable_row_sees_its_arguments), CurrentTest.Value?.MethodName);
    }

    /// <summary>
    ///     Rows xUnit cannot serialize run inside ONE test case, and each is still its own test with
    ///     its own display name.
    /// </summary>
    [ConditionalTheory]
    [MemberData(nameof(Unserializable))]
    public void A_row_xunit_cannot_serialize_sees_its_own_arguments(Opaque value)
        => Assert.Equal(
            $"{typeof(CurrentTestTest).FullName}.{nameof(A_row_xunit_cannot_serialize_sees_its_own_arguments)}(value: {value})",
            CurrentTest.Value?.DisplayName);

    /// <summary>Two rows of a type xUnit cannot serialize, so it falls back to one test case.</summary>
    public static IEnumerable<object[]> Unserializable
        => [[new Opaque(1)], [new Opaque(2)]];

    /// <summary>A class fixture is built before any test starts, so a fixture's seeding is nobody's.</summary>
    [ConditionalFact]
    public void A_class_fixture_is_built_outside_any_test()
    {
        Assert.True(fixture.Constructed);
        Assert.Null(fixture.CurrentTestInConstructor);
    }

    /// <summary>
    ///     The test class is built inside its test, because xUnit queues <c>ITestStarting</c> first,
    ///     so a statement its constructor runs is the test's.
    /// </summary>
    [ConditionalFact]
    public void A_test_class_is_built_inside_its_test()
        => Assert.Equal(
            $"{typeof(CurrentTestTest).FullName}.{nameof(A_test_class_is_built_inside_its_test)}",
            _nameInConstructor);

    /// <summary>The value reaches the server's command interceptor through the wire.</summary>
    [ConditionalFact]
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
        _ = await client.Tickets.ToListAsync();

        Assert.Equal(
            [$"{typeof(CurrentTestTest).FullName}.{nameof(A_statement_the_server_runs_carries_the_display_name)}"],
            names.Names);
    }

    /// <summary>A theory argument that xUnit cannot serialize.</summary>
    public sealed class Opaque(int number)
    {
        /// <inheritdoc />
        public override string ToString()
            => $"opaque {number}";
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
