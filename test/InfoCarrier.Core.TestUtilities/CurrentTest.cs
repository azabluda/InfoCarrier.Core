// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The test running in this async flow, for code the test cannot reach: the server's command
///     interceptor, deep inside EF, files each statement under it (#167).
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
        Ordinal = SqlCapture.NextOrdinal(TestClass, DisplayName);
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
    ///     1, unless an earlier test of the same class had the same <see cref="DisplayName" />: then
    ///     2, 3, … in the order they ran, which xUnit keeps fixed within a class.
    /// </summary>
    public int Ordinal { get; }

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
    ///     <c>After</c> calls it, because a test class's <c>Dispose</c> runs after <c>After</c>, and
    ///     a statement there would belong to no entry of a capture.
    /// </remarks>
    public void Close()
    {
        lock (_gate)
        {
            _closed = true;
        }
    }

    internal static CurrentTest Start(ITest test)
        => Current.Value = new CurrentTest(test);

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
///     about the statement's shape, and the test asserts the rows already (<c>docs/sql-capture.md</c>
///     §4.2).
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

    /// <summary>The command text as the server ran it. A capture normalizes it when it writes or compares.</summary>
    public string Text { get; }

    /// <summary>Which execute method ran it.</summary>
    public CapturedCommandKind Kind { get; }

    /// <summary>
    ///     For a reader, EF's own <c>ReadCount</c>, which counts every <c>Read()</c>, the last one
    ///     that returns false included, so three rows read to the end are four reads. For a
    ///     non-query, the rows it affected. Null for a scalar, a failed command, and a reader not
    ///     yet disposed.
    /// </summary>
    public int? Count { get; internal set; }

    /// <summary>The command threw.</summary>
    public bool Failed { get; }

    /// <summary>EF's correlation ID, which matches a reader's disposal to the command that opened it.</summary>
    internal Guid CommandId { get; }
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
///         <b>The bus also tells <see cref="SqlCapture" /> how each test ended</b>, and the executor
///         counts each class's test cases so that the wrapper of the last one can say the class has
///         ended. A capture run writes a class's file then (#167).
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
            Dictionary<Type, StrongBox<int>> remaining = cases
                .Select(ClassOf)
                .OfType<Type>()
                .GroupBy(type => type)
                .ToDictionary(group => group.Key, group => new StrongBox<int>(group.Count()));

            base.RunTestCases(
                [.. cases.Select(testCase => (IXunitTestCase)new TestCase(
                    testCase,
                    ClassOf(testCase) is { } type ? (type, remaining[type]) : null))],
                executionMessageSink,
                executionOptions);
        }

        private static Type? ClassOf(IXunitTestCase testCase)
            => (testCase.TestMethod.TestClass.Class as IReflectionTypeInfo)?.Type;
    }

    /// <summary>
    ///     The test case it wraps, in every member but one: the bus it runs on. It also counts down
    ///     its class's test cases, and the last one to return says that the class has ended.
    /// </summary>
    private sealed class TestCase(IXunitTestCase inner, (Type Class, StrongBox<int> Remaining)? testClass) : IXunitTestCase
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
            // SPIKE (#167, ADR-014): in a slow run the test case runs first with plain EF, on a
            // second set of class fixtures, and its wire run then compares with that.
            LiveComparison.DirectRun? direct = null;
            if (LiveComparison.IsEnabled && testClass is { } covered && LiveComparison.Covers(covered.Class))
            {
                direct = await LiveComparison.RunDirectAsync(
                    inner, covered.Class, diagnosticMessageSink, constructorArguments);
            }

            RunSummary summary = await inner.RunAsync(
                diagnosticMessageSink,
                new MessageBus(messageBus, direct),
                constructorArguments,
                aggregator,
                cancellationTokenSource);

            if (testClass is var (type, remaining) && Interlocked.Decrement(ref remaining.Value) == 0)
            {
                SqlCapture.ClassFinished(type);
                await LiveComparison.ClassFinishedAsync(type);
            }

            return summary;
        }
    }

    /// <summary>
    ///     The bus it wraps, which sets <see cref="CurrentTest" /> as each test starts, and tells
    ///     <see cref="SqlCapture" /> how each test that ran ended.
    /// </summary>
    /// <remarks>
    ///     One bus serves one test case, whose tests run one after another, so one test at a time is
    ///     tracked here. A skipped test runs no statement and gets no entry.
    /// </remarks>
    private sealed class MessageBus(IMessageBus inner, LiveComparison.DirectRun? direct = null) : IMessageBus
    {
        private CurrentTest? _test;
        private bool _failed;
        private bool _skipped;
        private string _outcome = "unknown";
        private int _index;

        public bool QueueMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _test = CurrentTest.Start(starting.Test);
                    _failed = false;
                    _skipped = false;
                    _outcome = "unknown";
                    break;

                case ITestPassed:
                    _outcome = "passed";
                    break;

                case ITestFailed failed:
                    _failed = true;
                    _outcome = $"failed: {failed.ExceptionTypes.FirstOrDefault()}";
                    break;

                case ITestSkipped:
                    _skipped = true;
                    _outcome = "skipped";
                    break;

                case ITestFinished when _test is not null:
                    if (!_skipped)
                    {
                        SqlCapture.TestFinished(_test, _failed);
                    }

                    if (direct is not null)
                    {
                        _test.Close();
                        LiveComparison.Report(_test, direct, _index, _outcome);
                    }

                    _index++;
                    _test = null;
                    break;
            }

            return inner.QueueMessage(message);
        }

        // The bus belongs to the runner that handed it over, and that runner disposes it.
        public void Dispose()
        {
        }
    }
}
