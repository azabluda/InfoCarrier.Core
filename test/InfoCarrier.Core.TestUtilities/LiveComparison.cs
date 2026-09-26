// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     SPIKE, NOT FOR MERGE (#167, ADR-014): each test runs twice in one process, first with plain EF
///     Core on a store of its own, then through InfoCarrier, and the two runs' server statements are
///     compared in memory.
/// </summary>
/// <remarks>
///     <para>
///         <b>Switched on by <see cref="Variable" />, which names a folder</b> for the report. Unset,
///         nothing here runs and a test runs once.
///     </para>
///     <para>
///         <b>The direct run gets a second set of class fixtures</b>, created and initialized with
///         <see cref="DirectClient" /> set, and disposed when the class's last test case returns. Its
///         messages are swallowed; its statements and its outcome are kept for the wire run.
///     </para>
///     <para>
///         <b>The spike reports and never fails a test.</b> Failing one from <c>After</c> is what H1d
///         proved on <c>sql-capture</c>; what the spike has to show is that the two runs can be made
///         and matched.
///     </para>
/// </remarks>
public static class LiveComparison
{
    /// <summary>The environment variable that switches the slow mode on, naming the report's folder.</summary>
    public const string Variable = "INFOCARRIER_LIVE_COMPARE";

    private static readonly string? Folder
        = Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } folder ? folder : null;

    private static readonly ConcurrentDictionary<Type, DirectFixtures> Fixtures = new();
    private static readonly object ReportGate = new();

    /// <summary>Whether this run is a slow one.</summary>
    public static bool IsEnabled => Folder is not null;

    /// <summary>
    ///     Whether the class runs twice: Tier B only, because only SQLite has a direct client.
    /// </summary>
    public static bool Covers(Type testClass)
        => testClass.Namespace is { } space
            && (space == "InfoCarrier.Core.FunctionalTests.Sqlite"
                || space.StartsWith("InfoCarrier.Core.FunctionalTests.Sqlite.", StringComparison.Ordinal));

    /// <summary>
    ///     Runs the test case with plain EF, on the class's direct fixtures, and returns what each of
    ///     its tests ran and how it ended.
    /// </summary>
    internal static async Task<DirectRun> RunDirectAsync(
        IXunitTestCase testCase,
        Type testClass,
        IMessageSink diagnosticMessageSink,
        object[] constructorArguments)
    {
        DirectFixtures fixtures = Fixtures.GetOrAdd(testClass, type => new DirectFixtures(type));
        var results = new List<DirectResult>();

        // Restored when this method returns: an async method restores its caller's async-locals.
        DirectClient.Set(true);

        object[] arguments;
        try
        {
            arguments = await fixtures.ArgumentsAsync(constructorArguments);
        }
        catch (Exception e)
        {
            return new DirectRun(results, e);
        }

        using var bus = new DirectBus(results);
        await testCase.RunAsync(
            diagnosticMessageSink, bus, arguments, new ExceptionAggregator(), new CancellationTokenSource());
        return new DirectRun(results, null);
    }

    /// <summary>Disposes the class's direct fixtures. Never throws.</summary>
    internal static async Task ClassFinishedAsync(Type testClass)
    {
        if (Fixtures.TryRemove(testClass, out DirectFixtures? fixtures))
        {
            await fixtures.DisposeAsync();
        }
    }

    /// <summary>
    ///     Compares one wire test with its direct run, once its outcome is known, and says whether
    ///     it is red (ADR-014 decision 3). Never throws.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>Red, per row:</b> the row differs from plain EF and its method carries no
    ///         <see cref="InfoCarrierDesignAttribute" /> or <see cref="InfoCarrierDefectAttribute" />;
    ///         or the row has no direct run to compare with.
    ///     </para>
    ///     <para>
    ///         <b>Red, per method, at its last row:</b> the method carries such a reason and none of
    ///         its rows differed, so the reason suppresses nothing.
    ///     </para>
    ///     <para>
    ///         SPIKE: a reason's <c>Case</c> is not read, so a reason covers every row of its method.
    ///     </para>
    /// </remarks>
    internal static Judgement Judge(CurrentTest test, DirectRun direct, int index, string wireOutcome, bool lastRowOfMethod)
    {
        try
        {
            test.Close();
            DirectResult? other = index < direct.Results.Count ? direct.Results[index] : null;
            SqlCaptureCommand[] wire = [.. test.Commands.Select(SqlCapture.ToFileCommand)];
            SqlCaptureCommand[] plain = other is null ? [] : [.. other.Commands.Select(SqlCapture.ToFileCommand)];
            string directOutcome = direct.FixtureFailure is { } failure
                ? $"fixture failed: {failure.GetType().FullName}: {FirstLine(failure.Message)}"
                : other?.Outcome ?? "no direct run";

            string verdict = direct.FixtureFailure is not null || other is null ? "no-direct"
                : other.DisplayName != test.DisplayName ? "unmatched"
                : Verdict(plain, wire, directOutcome, wireOutcome);

            (Type, string) method = (test.TestClass, test.MethodName);
            MethodState state = MethodStates.GetOrAdd(method, _ => new MethodState());
            bool anyRowDiffers;
            lock (state)
            {
                state.Differing += verdict == "same" ? 0 : 1;
                anyRowDiffers = state.Differing > 0;
            }

            if (lastRowOfMethod)
            {
                MethodStates.TryRemove(method, out _);
            }

            bool reason = HasInfoCarrierReason(test.TestClass, test.MethodName);
            string name = $"{test.TestClass.Name}.{test.MethodName}";
            string sides = $"\n\nPlain EF Core ({directOutcome}):\n{Show(plain)}\nInfoCarrier ({wireOutcome}):\n{Show(wire)}";
            (string? label, string? red) = verdict switch
            {
                "no-direct" or "unmatched" => ("no-direct-run", $"The slow run has no plain-EF run to compare this test with ({verdict}: {directOutcome})."),
                "same" when lastRowOfMethod && reason && !anyRowDiffers => (
                    "reason-without-difference",
                    $"{name} carries an [InfoCarrierDesign] or [InfoCarrierDefect] reason, and no row of it differs from plain EF Core: the reason suppresses nothing, so it goes.{sides}"),
                "same" => (null, null),
                _ when !reason => (
                    "difference-without-reason",
                    $"This test runs differently through InfoCarrier than with plain EF Core ({verdict}), and {name} carries no [InfoCarrierDesign] or [InfoCarrierDefect] reason.{sides}"),
                _ => (null, null),
            };

            return new Judgement(verdict, directOutcome, wireOutcome, plain, wire, label, red);
        }
        catch (Exception e)
        {
            return new Judgement("error", "?", wireOutcome, [], [], "comparison-error", $"The live comparison failed: {e}");
        }
    }

    /// <summary>Appends a judged test to the report. Never throws.</summary>
    internal static void Report(CurrentTest test, Judgement judgement)
    {
        try
        {
            string line = string.Join(
                '\t',
                test.TestClass.FullName,
                test.MethodName,
                test.DisplayName,
                judgement.Verdict,
                judgement.DirectOutcome,
                judgement.WireOutcome,
                judgement.Plain.Count,
                judgement.Wire.Count,
                judgement.RedLabel ?? string.Empty);

            lock (ReportGate)
            {
                Directory.CreateDirectory(Folder!);
                File.AppendAllText(Path.Combine(Folder!, "live-compare.tsv"), line + "\n");
                if (judgement.Verdict != "same" || judgement.RedLabel is not null)
                {
                    File.AppendAllText(
                        Path.Combine(Folder!, "live-compare-details.txt"),
                        $"===== {test.DisplayName}\n{judgement.Verdict} {judgement.RedLabel}\n--- direct ({judgement.DirectOutcome})\n{Show(judgement.Plain)}--- wire ({judgement.WireOutcome})\n{Show(judgement.Wire)}\n");
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"LiveComparison could not report {test.DisplayName}: {e}");
            Environment.ExitCode = 1;
        }
    }

    private static readonly ConcurrentDictionary<(Type, string), MethodState> MethodStates = new();
    private static readonly ConcurrentDictionary<(Type, string), bool> Reasons = new();

    private static bool HasInfoCarrierReason(Type testClass, string methodName)
        => Reasons.GetOrAdd(
            (testClass, methodName),
            key => key.Item1
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Where(m => m.Name == key.Item2)
                .Any(m => m.GetCustomAttributes(inherit: true).Any(a => a is InfoCarrierDesignAttribute or InfoCarrierDefectAttribute)));

    private sealed class MethodState
    {
        public int Differing { get; set; }
    }

    /// <summary>What the comparison found for one wire test, and why it is red, if it is.</summary>
    internal sealed record Judgement(
        string Verdict,
        string DirectOutcome,
        string WireOutcome,
        IReadOnlyList<SqlCaptureCommand> Plain,
        IReadOnlyList<SqlCaptureCommand> Wire,
        string? RedLabel,
        string? Red);

    /// <summary>
    ///     "same", or each way the two runs differ: <c>reads</c> and <c>writes</c> when the
    ///     statements themselves differ, <c>order</c> when only their order does, <c>failmark</c>,
    ///     <c>counts</c> for a reader's reads or a non-query's rows, and <c>outcome</c>.
    /// </summary>
    public static string Verdict(
        IReadOnlyList<SqlCaptureCommand> plain,
        IReadOnlyList<SqlCaptureCommand> wire,
        string directOutcome,
        string wireOutcome)
    {
        ArgumentNullException.ThrowIfNull(plain);
        ArgumentNullException.ThrowIfNull(wire);

        var flags = new List<string>();
        bool reads = !SameBag(plain.Where(c => IsRead(c.Text)), wire.Where(c => IsRead(c.Text)));
        bool writes = !SameBag(plain.Where(c => !IsRead(c.Text)), wire.Where(c => !IsRead(c.Text)));
        bool sameTexts = plain.Select(c => c.Text).SequenceEqual(wire.Select(c => c.Text), StringComparer.Ordinal);

        if (reads)
        {
            flags.Add("reads");
        }

        if (writes)
        {
            flags.Add("writes");
        }

        if (!reads && !writes && !sameTexts)
        {
            flags.Add("order");
        }

        if (sameTexts)
        {
            if (!SqlCapture.SameStatements(plain, wire))
            {
                flags.Add("failmark");
            }
            else if (!plain.Select(c => c.Outcome).SequenceEqual(wire.Select(c => c.Outcome), StringComparer.Ordinal))
            {
                flags.Add("counts");
            }
        }

        if (directOutcome != wireOutcome)
        {
            flags.Add("outcome");
        }

        return flags.Count == 0 ? "same" : string.Join('+', flags);
    }

    // A read is a statement whose first line that is not a comment starts SELECT or WITH.
    private static bool IsRead(string text)
    {
        string first = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("--", StringComparison.Ordinal)) ?? "";
        return first.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || first.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameBag(IEnumerable<SqlCaptureCommand> x, IEnumerable<SqlCaptureCommand> y)
        => x.Select(c => c.Text).Order(StringComparer.Ordinal)
            .SequenceEqual(y.Select(c => c.Text).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string Show(IReadOnlyList<SqlCaptureCommand> commands)
    {
        var text = new StringBuilder();
        for (int i = 0; i < commands.Count; i++)
        {
            text.Append($"-- #{i + 1} {commands[i].Outcome}\n").Append(commands[i].Text).Append('\n');
        }

        return text.ToString();
    }

    internal static string FirstLine(string? text)
        => (text ?? string.Empty).Split('\n')[0].Trim();

    /// <summary>What the direct run of one test case produced.</summary>
    internal sealed record DirectRun(IReadOnlyList<DirectResult> Results, Exception? FixtureFailure);

    /// <summary>One test of a direct run: its name, its statements, and how it ended.</summary>
    internal sealed record DirectResult(string DisplayName, IReadOnlyList<CapturedCommand> Commands, string Outcome);

    /// <summary>
    ///     A class's second set of class fixtures, for the direct run: created and initialized on the
    ///     class's first test case, disposed after its last.
    /// </summary>
    private sealed class DirectFixtures(Type testClass)
    {
        private Dictionary<Type, object>? _fixtures;

        public async Task<object[]> ArgumentsAsync(object[] constructorArguments)
        {
            if (_fixtures is null)
            {
                var created = new Dictionary<Type, object>();
                foreach (Type fixtureType in testClass.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IClassFixture<>))
                    .Select(i => i.GenericTypeArguments[0]))
                {
                    object fixture = Activator.CreateInstance(fixtureType)
                        ?? throw new InvalidOperationException($"'{fixtureType}' could not be created.");
                    created[fixtureType] = fixture;
                    if (fixture is IAsyncLifetime lifetime)
                    {
                        await lifetime.InitializeAsync();
                    }
                }

                _fixtures = created;
            }

            return [.. constructorArguments.Select(
                argument => argument is not null && _fixtures.TryGetValue(argument.GetType(), out object? direct) ? direct! : argument!)];
        }

        public async Task DisposeAsync()
        {
            foreach (object fixture in _fixtures?.Values.ToArray() ?? [])
            {
                try
                {
                    if (fixture is IAsyncLifetime lifetime)
                    {
                        await lifetime.DisposeAsync();
                    }

                    (fixture as IDisposable)?.Dispose();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"LiveComparison could not dispose the direct '{fixture.GetType()}': {e}");
                }
            }
        }
    }

    /// <summary>The direct run's bus: it sets <see cref="CurrentTest" />, keeps each test's result, and forwards nothing.</summary>
    private sealed class DirectBus(List<DirectResult> results) : IMessageBus
    {
        private CurrentTest? _test;
        private string _outcome = "unknown";

        public bool QueueMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _test = CurrentTest.Start(starting.Test);
                    _outcome = "unknown";
                    break;

                case ITestPassed:
                    _outcome = "passed";
                    break;

                case ITestFailed failed:
                    _outcome = $"failed: {failed.ExceptionTypes.FirstOrDefault()}";
                    break;

                case ITestSkipped:
                    _outcome = "skipped";
                    break;

                case ITestFinished when _test is not null:
                    _test.Close();
                    results.Add(new DirectResult(_test.DisplayName, _test.Commands, _outcome));
                    _test = null;
                    break;
            }

            return true;
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>SPIKE (#167): the failure a slow run reports for a test the live comparison turned red.</summary>
public sealed class LiveComparisonException(string message) : XunitException(message);
