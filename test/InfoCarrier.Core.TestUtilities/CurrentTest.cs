// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The test running in this async flow, for code the test cannot reach: the server's command
///     interceptor, deep inside EF, files each statement under it (#167, ADR-014).
/// </summary>
/// <remarks>
///     <para>
///         <b>Set by <see cref="CurrentTestFramework" />, and only in an assembly that names it</b>
///         with <c>[assembly: TestFramework]</c>. Anywhere else <see cref="Value" /> is null.
///     </para>
///     <para>
///         <b>Null outside a test</b>, and that is part of the contract: a class fixture is built
///         before its class's first test starts, so a shared store's seeding belongs to no test.
///     </para>
/// </remarks>
public sealed class CurrentTest
{
    private static readonly AsyncLocal<CurrentTest?> Current = new();

    private readonly object _gate = new();
    private readonly List<CapturedCommand> _commands = [];
    private bool _closed;

    private CurrentTest(ITest test)
    {
        DisplayName = test.DisplayName;
        TestClass = (test.TestCase.TestMethod.TestClass.Class as IReflectionTypeInfo)?.Type
            ?? throw new InvalidOperationException($"'{test.DisplayName}' has no runtime test class.");
        MethodName = test.TestCase.TestMethod.Method.Name;
    }

    /// <summary>The test running in this async flow, or null outside one.</summary>
    public static CurrentTest? Value => Current.Value;

    /// <summary>
    ///     The name the test explorer and the TRX show, with the class and the arguments:
    ///     <c>Ns.Class.Method(async: True)</c>.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>The class xUnit runs, which is not the base that declares an inherited test.</summary>
    public Type TestClass { get; }

    /// <summary>The test method's name, without the class and the arguments.</summary>
    public string MethodName { get; }

    /// <summary>
    ///     The commands the server ran for this test until <see cref="Close" />, in the order it ran
    ///     them. <c>ServerSqlRecordingInterceptor</c> files them.
    /// </summary>
    public IReadOnlyList<CapturedCommand> Commands
    {
        get
        {
            lock (_gate)
            {
                return [.. _commands];
            }
        }
    }

    /// <summary>
    ///     Ends the test's statements: a command the server runs later is not recorded, and a
    ///     reader disposed later leaves its count as it was.
    /// </summary>
    /// <remarks>
    ///     <see cref="CloseCurrentTestAttribute" /> calls it in <c>After</c>, because a test class's
    ///     <c>DisposeAsync</c> and <c>Dispose</c> run after <c>After</c> in the same async flow, and a
    ///     statement there is the class's cleaning up and not the test.
    /// </remarks>
    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
        }
    }

    /// <summary>
    ///     Whether a request crossed the wire during this test while <see cref="DirectClient" /> was
    ///     set, which means its "plain EF" run was not plain EF (#167).
    /// </summary>
    internal bool UsedWire { get; private set; }

    internal static CurrentTest Start(ITest test)
        => Current.Value = new CurrentTest(test);

    internal void NoteWire()
        => UsedWire = true;

    internal void Add(CapturedCommand command)
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _commands.Add(command);
            }
        }
    }

    internal void SetReadCount(Guid commandId, int readCount)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            for (int i = _commands.Count - 1; i >= 0; i--)
            {
                if (_commands[i].CommandId == commandId)
                {
                    _commands[i].Count = readCount;
                    return;
                }
            }
        }
    }
}

/// <summary>Which of <c>DbCommand</c>'s three execute methods the server called.</summary>
public enum CapturedCommandKind
{
    /// <summary><c>ExecuteReader</c>: a query, and a write that reads back what the store generated.</summary>
    Reader,

    /// <summary><c>ExecuteNonQuery</c>.</summary>
    NonQuery,

    /// <summary><c>ExecuteScalar</c>.</summary>
    Scalar,
}

/// <summary>One command the server ran for the current test, and how it ended.</summary>
/// <remarks>
///     <b>Never the parameter values or the rows</b>: they vary from run to run and say nothing
///     about the statement's shape, and the test asserts the rows already.
/// </remarks>
public sealed class CapturedCommand
{
    /// <summary>A command as the interceptor saw it, for a test that builds one by hand.</summary>
    public CapturedCommand(string text, CapturedCommandKind kind, int? count, bool failed)
        : this(Guid.Empty, text, kind, count, failed)
    {
    }

    internal CapturedCommand(Guid commandId, string text, CapturedCommandKind kind, int? count, bool failed)
    {
        CommandId = commandId;
        Text = text;
        Kind = kind;
        Count = count;
        Failed = failed;
    }

    /// <summary>The command text as the server ran it. <see cref="SqlNormalizer" /> makes it comparable.</summary>
    public string Text { get; }

    /// <summary>Which execute method ran it.</summary>
    public CapturedCommandKind Kind { get; }

    /// <summary>
    ///     For a reader, EF's own <c>ReadCount</c>, which counts every <c>Read()</c>, the last one
    ///     that returns false included, so three rows read to the end are four reads. For a
    ///     non-query, the rows it affected. Null for a scalar, a failed command, and a reader not
    ///     yet disposed.
    /// </summary>
    /// <remarks>
    ///     <b>EF's count misses rows in one shape, and the slow run of Phase H3 replaces it for that
    ///     reason.</b> <c>GroupBySingleQueryingEnumerable</c> reads the first row of each group
    ///     through <c>RelationalDataReader.Read</c>, which counts, and the rest through the raw
    ///     <c>DbDataReader</c>, which does not: a final <c>GroupBy</c> of 92 rows reports 2.
    /// </remarks>
    public int? Count { get; internal set; }

    /// <summary>The command threw.</summary>
    public bool Failed { get; }

    /// <summary>EF's correlation ID, which matches a reader's disposal to the command that opened it.</summary>
    internal Guid CommandId { get; }
}

/// <summary>
///     Closes <see cref="CurrentTest" /> in <c>After</c>, before the test class is disposed. Applied
///     to the assembly beside <c>[assembly: TestFramework]</c>.
/// </summary>
/// <remarks>
///     <b>An assembly attribute, which xUnit 2.9.3 honours</b>: <c>XunitTestCaseRunner</c> collects
///     <see cref="BeforeAfterTestAttribute" />s from the test collection, the class, the method and the
///     assembly.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CloseCurrentTestAttribute : BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest)
        => CurrentTest.Value?.Close();
}

/// <summary>
///     xUnit v2's own test framework, except that each test runs with <see cref="CurrentTest" /> set.
/// </summary>
/// <remarks>
///     <para>
///         <b>Why a test framework and not a <see cref="BeforeAfterTestAttribute" /></b>:
///         <see cref="BeforeAfterTestAttribute.Before" /> receives the method and never the arguments,
///         and xUnit v2 has no <c>TestContext</c>. This is v2's supported extension point, and it
///         delegates every member, so discovery, EF's <c>ConditionalFact</c> and
///         <c>ConditionalTheory</c>, and the test explorer see the test cases they would see without
///         it.
///     </para>
///     <para>
///         <b>Why the message bus.</b> Each test case is wrapped, and the wrapper wraps the
///         <see cref="IMessageBus" /> the case is handed. xUnit's <c>TestRunner</c> queues
///         <see cref="ITestStarting" /> synchronously and then awaits the test class's constructor,
///         <c>Before</c>, the method, <c>After</c> and the disposal in the same async flow, so a value
///         set while the message is queued holds for exactly one test, and the runner's own async
///         method restores the previous value when it returns. <b>A theory whose rows xUnit cannot
///         serialize is one test case whose rows run inside it</b>, and each row is still its own
///         <see cref="ITest" /> with its own display name, so each row is named here as well.
///     </para>
///     <para>
///         <b>In a slow run it also runs each covered test case twice</b>
///         (<see cref="LiveComparison" />, #167): first with plain EF Core, on a second set of class
///         fixtures, and then as usual, when the bus judges each test as its result arrives. It
///         counts each class's test cases, to dispose the second set after the last one, and each
///         method's, to know the method's last row.
///     </para>
///     <para>
///         <b>When EF's specification packages move to xUnit v3, <c>TestContext.Current.Test</c>
///         replaces this class.</b> <c>Microsoft.EntityFrameworkCore.Specification.Tests</c> 10.0.1
///         depends on <c>xunit.core</c> 2.9.3, which is what keeps this suite on v2.
///     </para>
/// </remarks>
public sealed class CurrentTestFramework(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    /// <inheritdoc />
    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new Executor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);

    private sealed class Executor(
        AssemblyName assemblyName,
        ISourceInformationProvider sourceInformationProvider,
        IMessageSink diagnosticMessageSink)
        : XunitTestFrameworkExecutor(assemblyName, sourceInformationProvider, diagnosticMessageSink)
    {
        protected override void RunTestCases(
            IEnumerable<IXunitTestCase> testCases,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions)
        {
            IXunitTestCase[] cases = [.. testCases];

            // The test cases of one class run one after another, and so do those of one method.
            Dictionary<Type, StrongBox<int>> inClass = cases
                .Select(ClassOf)
                .OfType<Type>()
                .GroupBy(type => type)
                .ToDictionary(group => group.Key, group => new StrongBox<int>(group.Count()));
            Dictionary<(Type, string), StrongBox<int>> inMethod = cases
                .Where(testCase => ClassOf(testCase) is not null)
                .GroupBy(testCase => (ClassOf(testCase)!, testCase.TestMethod.Method.Name))
                .ToDictionary(group => group.Key, group => new StrongBox<int>(group.Count()));

            base.RunTestCases(
                [.. cases.Select(testCase => (IXunitTestCase)new TestCase(
                    testCase,
                    ClassOf(testCase) is { } type
                        ? new Countdown(type, inClass[type], inMethod[(type, testCase.TestMethod.Method.Name)])
                        : null))],
                executionMessageSink,
                executionOptions);
        }

        private static Type? ClassOf(IXunitTestCase testCase)
            => (testCase.TestMethod.TestClass.Class as IReflectionTypeInfo)?.Type;
    }

    /// <summary>A test case's class, and how many test cases of its class and of its method are left.</summary>
    private sealed record Countdown(Type Class, StrongBox<int> InClass, StrongBox<int> InMethod);

    /// <summary>
    ///     The test case it wraps, in every member but one: the bus it runs on. In a slow run it runs
    ///     the case with plain EF first.
    /// </summary>
    private sealed class TestCase(IXunitTestCase inner, Countdown? countdown) : IXunitTestCase
    {
        public string DisplayName => inner.DisplayName;

        public string SkipReason => inner.SkipReason;

        public ISourceInformation SourceInformation
        {
            get => inner.SourceInformation;
            set => inner.SourceInformation = value;
        }

        public ITestMethod TestMethod => inner.TestMethod;

        public object[] TestMethodArguments => inner.TestMethodArguments;

        public Dictionary<string, List<string>> Traits => inner.Traits;

        public string UniqueID => inner.UniqueID;

        public Exception InitializationException => inner.InitializationException;

        public IMethodInfo Method => inner.Method;

        public int Timeout => inner.Timeout;

        public void Deserialize(IXunitSerializationInfo info)
            => inner.Deserialize(info);

        public void Serialize(IXunitSerializationInfo info)
            => inner.Serialize(info);

        public async Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            bool slow = LiveComparison.IsEnabled && countdown is not null && LiveComparison.Covers(countdown.Class);
            LiveComparison.DirectRun? direct = slow
                ? await LiveComparison.RunDirectAsync(inner, countdown!.Class, diagnosticMessageSink, constructorArguments)
                : null;

            bool lastCaseOfMethod = countdown is not null && Interlocked.Decrement(ref countdown.InMethod.Value) == 0;
            var bus = new MessageBus(messageBus, direct, lastCaseOfMethod);
            RunSummary summary = await inner.RunAsync(
                diagnosticMessageSink, bus, constructorArguments, aggregator, cancellationTokenSource);

            // A pass the comparison turned red is a failure in the summary as well.
            summary.Failed += bus.TurnedRed;

            if (countdown is not null && Interlocked.Decrement(ref countdown.InClass.Value) == 0 && slow)
            {
                await LiveComparison.ClassFinishedAsync(countdown.Class);
            }

            return summary;
        }
    }

    /// <summary>
    ///     The bus it wraps, which sets <see cref="CurrentTest" /> as each test starts, and in a slow
    ///     run judges each test as its result arrives.
    /// </summary>
    /// <remarks>
    ///     The result message is where the outcome is known, and where a pass can still be reported
    ///     as a failure: <c>After</c> runs before it and cannot know whether the test passed.
    /// </remarks>
    private sealed class MessageBus(IMessageBus inner, LiveComparison.DirectRun? direct, bool lastCaseOfMethod) : IMessageBus
    {
        private CurrentTest? _test;
        private int _index;

        /// <summary>How many passes the comparison reported as failures.</summary>
        public int TurnedRed { get; private set; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _test = CurrentTest.Start(starting.Test);
                    break;

                case ITestPassed passed when Judge("passed") is { } red:
                    TurnedRed++;
                    message = new TestFailed(passed.Test, passed.ExecutionTime, passed.Output, new LiveComparisonException(red));
                    break;

                case ITestFailed failed:
                    _ = Judge($"failed: {failed.ExceptionTypes.FirstOrDefault()}");
                    break;

                case ITestSkipped:
                    _ = Judge("skipped");
                    break;

                case ITestFinished:
                    _index++;
                    _test = null;
                    break;
            }

            return inner.QueueMessage(message);
        }

        // A failed or skipped test is judged too, for its method's last row, and stays as it is.
        private string? Judge(string outcome)
            => direct is null || _test is null
                ? null
                : LiveComparison.Judge(
                    _test,
                    direct,
                    _index,
                    outcome,
                    lastRowOfMethod: lastCaseOfMethod && _index >= direct.Results.Count - 1);

        // The bus belongs to the runner that handed it over, and that runner disposes it.
        public void Dispose()
        {
        }
    }
}
