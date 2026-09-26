// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The slow run of #167 (ADR-014): each test runs twice in one process, first with plain EF Core
///     on a store of its own and then through InfoCarrier, and the second run is compared with the
///     first when its result arrives.
/// </summary>
/// <remarks>
///     <para>
///         <b>On when <see cref="Variable" /> is <c>1</c>, and off in every normal run.</b> It writes
///         no file (owner, 2026-09-26): everything a red test has to say is in its failure message,
///         whose format <see cref="Message" /> documents.
///     </para>
///     <para>
///         <b>The plain-EF run gets a second set of class fixtures</b>, created and initialized with
///         <see cref="DirectClient" /> set, and disposed after the class's last test case. Its
///         messages are swallowed; its statements and its outcome are kept for the wire run, which
///         is matched with it by position inside the test case.
///     </para>
///     <para>
///         <b>Which tests</b> (ADR-014, amendment 2026-09-26): Tier B's classes that run an EF
///         specification base. This repository's own classes assert the provider directly and have
///         no plain-EF counterpart, and they run once.
///     </para>
///     <para>
///         <b>When a test is red</b>, by the same amendment. A row whose statements differ needs a
///         reason on its method flagged <see cref="DeviationKind.SqlDiffers" />; a row whose
///         outcome differs needs one flagged <see cref="DeviationKind.AnswerNotRefusal" /> or
///         <see cref="DeviationKind.RefusedEarlier" />, and its statements are then not judged on
///         their own, because they follow from the outcome. At a method's last row, such a reason with
///         no row that differs in its kind is red too. A skip is never read, and no other reason
///         is. A row with no plain-EF run to compare with is red. <b>A red is a defect report
///         first</b>: fixing the provider is the way to make it green, and a reason the fallback.
///     </para>
/// </remarks>
public static class LiveComparison
{
    /// <summary>The environment variable that switches the slow run on, when it is <c>1</c>.</summary>
    public const string Variable = "INFOCARRIER_LIVE_COMPARE";

    /// <summary>The first word of every red message, for a script to find it by.</summary>
    public const string Header = "[live-compare]";

    /// <summary>The last line of every red message.</summary>
    public const string Footer = "[/live-compare]";

    private const string TierBNamespace = "InfoCarrier.Core.FunctionalTests.Sqlite";

    private const DeviationKind OutcomeReasons = DeviationKind.AnswerNotRefusal | DeviationKind.RefusedEarlier;

    private static readonly ConcurrentDictionary<Type, DirectFixtures> Fixtures = new();
    private static readonly ConcurrentDictionary<Type, bool> Covered = new();
    private static readonly ConcurrentDictionary<(Type, string), MethodState> MethodStates = new();
    private static readonly ConcurrentDictionary<(Type, string), DeviationKind> Reasons = new();

    /// <summary>Whether this run is a slow one.</summary>
    public static bool IsEnabled { get; } = Environment.GetEnvironmentVariable(Variable) == "1";

    /// <summary>Whether the class runs twice in a slow run: a Tier B class that runs an EF specification base.</summary>
    public static bool Covers(Type testClass)
    {
        ArgumentNullException.ThrowIfNull(testClass);

        return Covered.GetOrAdd(
            testClass,
            type => type.Namespace is { } space
                && (space == TierBNamespace || space.StartsWith(TierBNamespace + ".", StringComparison.Ordinal))
                && RunsSpecificationBase(type));
    }

    /// <summary>
    ///     "same", or each way the two runs of one test differ, joined by <c>+</c>: <c>reads</c> and
    ///     <c>writes</c> when the statements themselves differ, <c>order</c> when only their order
    ///     does, <c>failmark</c>, <c>counts</c> for a reader's reads or a non-query's rows, and
    ///     <c>outcome</c>.
    /// </summary>
    public static string Verdict(
        IReadOnlyList<ComparedCommand> plain,
        IReadOnlyList<ComparedCommand> wire,
        string plainOutcome,
        string wireOutcome)
    {
        ArgumentNullException.ThrowIfNull(plain);
        ArgumentNullException.ThrowIfNull(wire);

        var flags = new List<string>();
        bool reads = !SameBag(plain.Where(c => c.IsRead), wire.Where(c => c.IsRead));
        bool writes = !SameBag(plain.Where(c => !c.IsRead), wire.Where(c => !c.IsRead));
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
            if (!plain.Select(c => c.Failed).SequenceEqual(wire.Select(c => c.Failed)))
            {
                flags.Add("failmark");
            }
            else if (!plain.Select(c => c.Outcome).SequenceEqual(wire.Select(c => c.Outcome), StringComparer.Ordinal))
            {
                flags.Add("counts");
            }
        }

        if (plainOutcome != wireOutcome)
        {
            flags.Add("outcome");
        }

        return flags.Count == 0 ? "same" : string.Join('+', flags);
    }

    /// <summary>
    ///     Runs the test case with plain EF, on the class's second set of fixtures, and returns what
    ///     each of its tests ran and how it ended.
    /// </summary>
    internal static async Task<DirectRun> RunDirectAsync(
        IXunitTestCase testCase,
        Type testClass,
        IMessageSink diagnosticMessageSink,
        object[] constructorArguments)
    {
        DirectFixtures fixtures = Fixtures.GetOrAdd(testClass, type => new DirectFixtures(type, diagnosticMessageSink));
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
        _ = await testCase.RunAsync(
            diagnosticMessageSink, bus, arguments, new ExceptionAggregator(), new CancellationTokenSource());
        return new DirectRun(results, null);
    }

    /// <summary>Disposes the class's second set of fixtures. Never throws.</summary>
    internal static async Task ClassFinishedAsync(Type testClass)
    {
        if (Fixtures.TryRemove(testClass, out DirectFixtures? fixtures))
        {
            await fixtures.DisposeAsync();
        }
    }

    /// <summary>
    ///     Compares one wire test with its plain-EF run, once its outcome is known, and returns the
    ///     failure message when the test is red, or null. Never throws.
    /// </summary>
    internal static string? Judge(CurrentTest test, DirectRun direct, int index, string wireOutcome, bool lastRowOfMethod)
    {
        try
        {
            test.Close();
            DirectResult? other = index < direct.Results.Count ? direct.Results[index] : null;
            ComparedCommand[] wire = [.. test.Commands.Select(ComparedCommand.From)];
            ComparedCommand[] plain = other is null ? [] : [.. other.Commands.Select(ComparedCommand.From)];
            string plainOutcome = direct.FixtureFailure is { } failure
                ? $"fixture failed: {failure.GetType().FullName}"
                : other?.Outcome ?? "no plain-EF run";

            string verdict = direct.FixtureFailure is not null || other is null ? "no-plain-ef-run"
                : other.DisplayName != test.DisplayName ? "unmatched"
                : other.UsedWire ? "plain-ef-run-used-infocarrier"
                : Verdict(plain, wire, plainOutcome, wireOutcome);

            // When the outcomes differ, the two runs did different things, so their statements are
            // the outcome's consequence and not judged on their own: plain EF refusing a query
            // InfoCarrier answers runs no statement where InfoCarrier runs several.
            string[] flags = verdict.Split('+');
            bool outcome = flags.Contains("outcome");
            bool statements = !outcome && flags.Any(f => f is "reads" or "writes" or "order" or "failmark" or "counts");

            (Type, string) method = (test.TestClass, test.MethodName);
            MethodState state = MethodStates.GetOrAdd(method, _ => new MethodState());
            bool anyStatements;
            bool anyOutcome;
            lock (state)
            {
                state.Statements |= statements;
                state.Outcome |= outcome;
                anyStatements = state.Statements;
                anyOutcome = state.Outcome;
            }

            if (lastRowOfMethod)
            {
                MethodStates.TryRemove(method, out _);
            }

            DeviationKind reasons = ReasonsOf(test.TestClass, test.MethodName);
            bool coversStatements = reasons.HasFlag(DeviationKind.SqlDiffers);
            bool coversOutcome = (reasons & OutcomeReasons) != 0;
            string name = $"{test.TestClass.Name}.{test.MethodName}";

            (string? label, string? why) =
                verdict is "no-plain-ef-run" or "unmatched" or "plain-ef-run-used-infocarrier"
                    ? ("no-plain-ef-run", $"The slow run has no plain-EF run of this test to compare with ({verdict}).")
                : (statements && !coversStatements) || (outcome && !coversOutcome)
                    ? ("difference-without-reason",
                        $"This test runs differently through InfoCarrier than with plain EF Core, and {name} carries no InfoCarrier reason flagged for it: "
                        + "SqlDiffers for a statement difference, AnswerNotRefusal or RefusedEarlier for an outcome difference.")
                : lastRowOfMethod && ((coversStatements && !anyStatements) || (coversOutcome && !anyOutcome))
                    ? ("reason-without-difference",
                        $"{name} carries an InfoCarrier reason flagged {reasons & (DeviationKind.SqlDiffers | OutcomeReasons)}, and no row of it differs in that way from plain EF Core, so the reason suppresses nothing.")
                : (null, null);

            return label is null
                ? null
                : Message(label, verdict, test.DisplayName, why!, plainOutcome, other?.FailureText ?? direct.FixtureFailure?.Message, plain, wireOutcome, wire);
        }
        catch (Exception e)
        {
            return $"{Header} comparison-error error {test.DisplayName}\nThe live comparison failed: {e}\n{Footer}";
        }
    }

    /// <summary>
    ///     The failure message of a red test: for a person to read in the console, and for a script
    ///     to parse out of the console or the TRX's <c>&lt;Message&gt;</c> element.
    /// </summary>
    /// <remarks>
    ///     <code>
    ///     [live-compare] &lt;label&gt; &lt;verdict&gt; &lt;display name&gt;
    ///     &lt;one sentence&gt;
    ///     --- plain EF Core: &lt;outcome&gt;, &lt;n&gt; statement(s)
    ///       | &lt;the plain-EF failure's text, up to six lines, when it failed&gt;
    ///     -- #1 reads 3
    ///     &lt;statement, normalized&gt;
    ///     --- InfoCarrier: &lt;outcome&gt;, &lt;n&gt; statement(s)
    ///     -- #1 ...
    ///     [/live-compare]
    ///     </code>
    ///     <para>
    ///         The label is <c>difference-without-reason</c>, <c>reason-without-difference</c> or
    ///         <c>no-plain-ef-run</c>. The label, the verdict and the display name are the rest of the
    ///         first line in that order, separated by one space; only the display name can contain
    ///         spaces. <b>No line of the message is blank</b>, because a normalized statement never has
    ///         one, so the console's blank line after a failure is where the message ends; the footer
    ///         says so as well.
    ///     </para>
    /// </remarks>
    public static string Message(
        string label,
        string verdict,
        string displayName,
        string why,
        string plainOutcome,
        string? plainFailureText,
        IReadOnlyList<ComparedCommand> plain,
        string wireOutcome,
        IReadOnlyList<ComparedCommand> wire)
    {
        ArgumentNullException.ThrowIfNull(plain);
        ArgumentNullException.ThrowIfNull(wire);

        var text = new StringBuilder()
            .Append($"{Header} {label} {verdict} {displayName}\n")
            .Append(why).Append('\n')
            .Append($"--- plain EF Core: {plainOutcome}, {Statements(plain.Count)}\n");

        if (plainOutcome != "passed" && plainFailureText is not null)
        {
            foreach (string line in plainFailureText.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Trim().Length > 0)
                .Take(6))
            {
                text.Append("  | ").Append(line.Length > 300 ? line[..300] + " ..." : line).Append('\n');
            }
        }

        return text
            .Append(Show(plain))
            .Append($"--- InfoCarrier: {wireOutcome}, {Statements(wire.Count)}\n")
            .Append(Show(wire))
            .Append(Footer)
            .ToString();
    }

    // A class runs a specification base when a type it derives from lives in one of EF's
    // specification assemblies: Microsoft.EntityFrameworkCore.Specification.Tests and
    // Microsoft.EntityFrameworkCore.Relational.Specification.Tests.
    private static bool RunsSpecificationBase(Type testClass)
    {
        for (Type? type = testClass.BaseType; type is not null; type = type.BaseType)
        {
            if (type.Assembly.GetName().Name is { } assembly
                && assembly.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal)
                && assembly.EndsWith(".Specification.Tests", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The flags of the method's InfoCarrier reasons that a slow run reads: this provider's
    // behaviour, never a skip, never another reason (ADR-014, amendment 2026-09-26).
    private static DeviationKind ReasonsOf(Type testClass, string methodName)
        => Reasons.GetOrAdd(
            (testClass, methodName),
            key => key.Item1
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == key.Item2)
                .SelectMany(m => m.GetCustomAttributes<OverrideReasonAttribute>(inherit: true))
                .Where(a => a is InfoCarrierDefectAttribute or InfoCarrierDesignAttribute { Skip: false })
                .Aggregate(DeviationKind.None, (all, a) => all | (a.Deviation & OverrideAudit.InfoCarrierBehaviour)));

    private static bool SameBag(IEnumerable<ComparedCommand> x, IEnumerable<ComparedCommand> y)
        => x.Select(c => c.Text).Order(StringComparer.Ordinal)
            .SequenceEqual(y.Select(c => c.Text).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static string Show(IReadOnlyList<ComparedCommand> commands)
    {
        var text = new StringBuilder();
        for (int i = 0; i < commands.Count; i++)
        {
            text.Append($"-- #{i + 1} {commands[i].Outcome}\n").Append(commands[i].Text).Append('\n');
        }

        return text.ToString();
    }

    private static string Statements(int count)
        => count == 1 ? "1 statement" : $"{count} statements";

    /// <summary>What the plain-EF run of one test case produced.</summary>
    internal sealed record DirectRun(IReadOnlyList<DirectResult> Results, Exception? FixtureFailure);

    /// <summary>One test of a plain-EF run: its name, its statements, how it ended, and whether it crossed the wire.</summary>
    internal sealed record DirectResult(
        string DisplayName, IReadOnlyList<CapturedCommand> Commands, string Outcome, string? FailureText, bool UsedWire);

    private sealed class MethodState
    {
        public bool Statements { get; set; }

        public bool Outcome { get; set; }
    }

    /// <summary>
    ///     A class's second set of class fixtures, for the plain-EF run: created and initialized on
    ///     the class's first test case, disposed after its last.
    /// </summary>
    private sealed class DirectFixtures(Type testClass, IMessageSink diagnosticMessageSink)
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
                    // xUnit v2 builds a class fixture with no arguments or with its message sink.
                    object fixture = fixtureType.GetConstructor([typeof(IMessageSink)]) is { } withSink
                        ? withSink.Invoke([diagnosticMessageSink])
                        : Activator.CreateInstance(fixtureType)
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
                    await Console.Error.WriteLineAsync($"{Header} could not dispose the plain-EF '{fixture.GetType()}': {e}");
                }
            }
        }
    }

    /// <summary>The plain-EF run's bus: it sets <see cref="CurrentTest" />, keeps each test's result, and forwards nothing.</summary>
    private sealed class DirectBus(List<DirectResult> results) : IMessageBus
    {
        private CurrentTest? _test;
        private string _outcome = "unknown";
        private string? _failureText;

        public bool QueueMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _test = CurrentTest.Start(starting.Test);
                    _outcome = "unknown";
                    _failureText = null;
                    break;

                case ITestPassed:
                    _outcome = "passed";
                    break;

                case ITestFailed failed:
                    _outcome = $"failed: {failed.ExceptionTypes.FirstOrDefault()}";
                    _failureText = failed.Messages.FirstOrDefault();
                    break;

                case ITestSkipped:
                    _outcome = "skipped";
                    break;

                case ITestFinished when _test is not null:
                    _test.Close();
                    results.Add(new DirectResult(_test.DisplayName, _test.Commands, _outcome, _failureText, _test.UsedWire));
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

/// <summary>One server command as a slow run compares it: its normalized text and how it ended.</summary>
/// <param name="Text">The command text, through <see cref="SqlNormalizer" />.</param>
/// <param name="Outcome"><c>failed</c>, <c>reads N</c>, <c>rows N</c> or <c>scalar</c>.</param>
/// <param name="Failed">The command threw.</param>
public sealed record ComparedCommand(string Text, string Outcome, bool Failed)
{
    /// <summary>Whether it reads: its first line that is not a comment starts <c>SELECT</c> or <c>WITH</c>.</summary>
    public bool IsRead
        => Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("--", StringComparison.Ordinal)) is { } first
            && (first.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || first.StartsWith("WITH", StringComparison.OrdinalIgnoreCase));

    /// <summary>The command as a slow run compares it.</summary>
    public static ComparedCommand From(CapturedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return new(
            SqlNormalizer.Normalize(command.Text),
            command.Failed ? "failed"
            : command.Kind switch
            {
                CapturedCommandKind.Reader => command.Count is { } reads ? $"reads {reads}" : "reads ?",
                CapturedCommandKind.NonQuery => $"rows {command.Count}",
                _ => "scalar",
            },
            command.Failed);
    }
}

/// <summary>The failure a slow run reports for a test the live comparison turned red.</summary>
public sealed class LiveComparisonException(string message) : XunitException(message);
