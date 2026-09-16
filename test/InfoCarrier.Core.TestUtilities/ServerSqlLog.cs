// Licensed under the MIT license. See license.txt file in the project root for license information.

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     An opt-in record of the SQL the <em>server</em> ran, for diagnosing a failing Tier B test.
/// </summary>
/// <remarks>
///     <para>
///         <b>Off unless <c>INFOCARRIER_SERVER_SQL</c> is set, and it asserts nothing.</b> Its
///         whole job is that a failing test can be re-run with the switch on and the statements
///         read back. Until #59 there was no way to see them at all:
///         <see cref="InfoCarrierTestStoreFactory" /> builds a
///         <c>TestSqlLoggerFactory</c>, but it belongs to the <em>client</em>, which has no
///         database and emits none, and <see cref="InfoCarrierBackendTestStore" /> wired no logger.
///     </para>
///     <para>
///         <b>Why a file and a switch rather than test output on failure.</b> EF attaches its own
///         SQL to a failing test through <c>TestSqlLoggerFactory.SetTestOutputHelper</c>, which
///         every relational base takes an <c>ITestOutputHelper</c> to supply. Reaching that here
///         would mean threading one through every Tier B test class, and xUnit gives a fixture no
///         way to know a test failed. A switch costs nothing when off, and the diagnostic loop —
///         re-run the one failing test with a filter, read the file — is the same loop anyone is
///         already in.
///     </para>
///     <para>
///         This is diagnosis, not coverage. The test that asserts the server's SQL is
///         <c>ServerParameterizationTest</c>, and it compares a statement against the same query
///         run directly rather than against a golden string.
///     </para>
/// </remarks>
public static class ServerSqlLog
{
    private static readonly object Gate = new();

    /// <summary>
    ///     Whether server SQL is being recorded.
    /// </summary>
    public static bool IsEnabled { get; }
        = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INFOCARRIER_SERVER_SQL"));

    /// <summary>
    ///     Where the statements are written.
    /// </summary>
    /// <remarks>
    ///     The test output directory, so it sits beside the <c>.db</c> files of the run that
    ///     produced it. Truncated once per process, on the first write.
    /// </remarks>
    public static string Path { get; }
        = System.IO.Path.Combine(AppContext.BaseDirectory, "server-sql.log");

    private static bool _started;

    /// <summary>
    ///     Records one statement.
    /// </summary>
    /// <param name="line">The formatted log line, as <c>LogTo</c> supplies it.</param>
    public static void Write(string line)
    {
        lock (Gate)
        {
            if (!_started)
            {
                System.IO.File.WriteAllText(Path, string.Empty);
                _started = true;
            }

            System.IO.File.AppendAllText(Path, line + Environment.NewLine + Environment.NewLine);
        }
    }

    /// <summary>
    ///     Writes the line that says which test the statements after it belong to.
    /// </summary>
    /// <param name="test">The test's class and method.</param>
    public static void WriteTestMarker(string test)
        => Write(TestMarker + test);

    /// <summary>The prefix of a test marker line, which <c>eng/ef-sql-diff.py</c> reads.</summary>
    public const string TestMarker = "=== TEST ";
}

/// <summary>
///     Marks each test's start in <see cref="ServerSqlLog" />, so the statements after it are known
///     to be that test's.
/// </summary>
/// <remarks>
///     <para>
///         <b>Applied to the assembly</b>, in <c>ServerSqlLogAssemblyInfo.cs</c>, and inert unless
///         <c>INFOCARRIER_SERVER_SQL</c> is set. It is what makes <c>eng/ef-sql-diff.py</c> possible:
///         the log is otherwise one stream with nothing saying where a test begins.
///     </para>
///     <para>
///         <b>A comparison run has to be serial</b> —
///         <c>dotnet test … -- xUnit.ParallelizeTestCollections=false</c> — or the statements of two
///         tests interleave between two markers. That is the caller's business, not this attribute's:
///         a parallel run with the switch on is still a usable diagnostic, as it was before markers
///         existed.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ServerSqlTestMarkerAttribute : Xunit.Sdk.BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void Before(System.Reflection.MethodInfo methodUnderTest)
    {
        ArgumentNullException.ThrowIfNull(methodUnderTest);

        if (ServerSqlLog.IsEnabled)
        {
            ServerSqlLog.WriteTestMarker($"{methodUnderTest.ReflectedType?.FullName}.{methodUnderTest.Name}");
        }
    }
}
