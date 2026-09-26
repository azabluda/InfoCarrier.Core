// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
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

    internal static void Start(ITest test)
        => Current.Value = new CurrentTest(test);
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
            => base.RunTestCases(
                [.. testCases.Select(testCase => (IXunitTestCase)new TestCase(testCase))],
                executionMessageSink,
                executionOptions);
    }

    /// <summary>
    ///     The test case it wraps, in every member but one: the bus it runs on.
    /// </summary>
    private sealed class TestCase(IXunitTestCase inner) : IXunitTestCase
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

        public Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            => inner.RunAsync(
                diagnosticMessageSink,
                new MessageBus(messageBus),
                constructorArguments,
                aggregator,
                cancellationTokenSource);
    }

    /// <summary>
    ///     The bus it wraps, which sets <see cref="CurrentTest" /> as each test starts.
    /// </summary>
    private sealed class MessageBus(IMessageBus inner) : IMessageBus
    {
        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (message is ITestStarting starting)
            {
                CurrentTest.Start(starting.Test);
            }

            return inner.QueueMessage(message);
        }

        // The bus belongs to the runner that handed it over, and that runner disposes it.
        public void Dispose()
        {
        }
    }
}
