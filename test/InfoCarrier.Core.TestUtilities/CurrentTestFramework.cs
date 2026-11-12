// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit.Sdk;
using Xunit.v3;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>Runs the optional live comparison; ordinary runs use xUnit's standard executor.</summary>
public sealed class CurrentTestFramework : XunitTestFramework
{
    /// <inheritdoc />
    protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly)
        => LiveComparison.IsEnabled
            ? new Executor(new XunitTestAssembly(assembly, configFilePath: null))
            : base.CreateExecutor(assembly);

    private sealed class Executor(IXunitTestAssembly assembly) : XunitTestFrameworkExecutor(assembly)
    {
        public override ValueTask RunTestCases(
            IReadOnlyCollection<IXunitTestCase> testCases,
            IMessageSink executionMessageSink,
            ITestFrameworkExecutionOptions executionOptions,
            CancellationToken cancellationToken)
        {
            var inClass = testCases.GroupBy(c => c.TestClass.Class)
                .ToDictionary(g => g.Key, g => new StrongBox<int>(g.Count()));
            var inMethod = testCases.GroupBy(c => (c.TestClass.Class, c.TestMethodName))
                .ToDictionary(g => g.Key, g => new StrongBox<int>(g.Count()));
            return base.RunTestCases(
                [.. testCases.Select(c => (IXunitTestCase)new ComparisonCase(
                    c, inClass[c.TestClass.Class], inMethod[(c.TestClass.Class, c.TestMethodName)]))],
                executionMessageSink, executionOptions, cancellationToken);
        }
    }

    // The original case still handles execution, including EF's conditional facts and delayed theories.
    private sealed class ComparisonCase(IXunitTestCase inner, StrongBox<int> inClass, StrongBox<int> inMethod)
        : XunitTestCase(
            inner.TestMethod, inner.TestCaseDisplayName, inner.UniqueID, inner.Explicit,
            (inner as XunitTestCase)?.TestLabel, inner.DisableParallelization,
            inner.SkipExceptions, inner.SkipReason, inner.SkipType, inner.SkipUnless, inner.SkipWhen,
            inner.Traits.ToDictionary(p => p.Key, p => p.Value.ToHashSet()),
            sourceFilePath: inner.SourceFilePath, sourceLineNumber: inner.SourceLineNumber, timeout: inner.Timeout),
            ISelfExecutingXunitTestCase
    {
        public override ValueTask<IReadOnlyCollection<IXunitTest>> CreateTests() => inner.CreateTests();
        public override void PreInvoke() => inner.PreInvoke();
        public override void PostInvoke() => inner.PostInvoke();

        public async ValueTask<RunSummary> Run(
            ExplicitOption explicitOption, IMessageBus messageBus, object?[] constructorArguments,
            ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource,
            ParallelMode parallelMode, ExecutionScheduler scheduler, FixtureMappingManager methodFixtureMappings)
        {
            Type type = inner.TestClass.Class;
            bool slow = LiveComparison.Covers(type);
            LiveComparison.DirectRun? direct = slow
                ? await LiveComparison.RunDirectAsync(inner, type, constructorArguments, parallelMode, scheduler)
                : null;
            bool lastCaseOfMethod = Interlocked.Decrement(ref inMethod.Value) == 0;
            using var bus = new ComparisonBus(messageBus, direct, lastCaseOfMethod);
            RunSummary summary = await XunitRunnerHelper.RunXunitTestCase(
                inner, bus, cancellationTokenSource, parallelMode, scheduler, aggregator,
                explicitOption, constructorArguments, methodFixtureMappings);
            summary.Failed += bus.TurnedRed;
            if (Interlocked.Decrement(ref inClass.Value) == 0 && slow)
            {
                await LiveComparison.ClassFinishedAsync(type);
            }
            return summary;
        }
    }

    private sealed class ComparisonBus(IMessageBus inner, LiveComparison.DirectRun? direct, bool lastCaseOfMethod) : IMessageBus
    {
        private int _index;
        public int TurnedRed { get; private set; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestPassed passed when Judge("passed") is { } red:
                    TurnedRed++;
                    message = TestFailed.FromException(new LiveComparisonException(red),
                        passed.AssemblyUniqueID, passed.TestCollectionUniqueID, passed.TestClassUniqueID,
                        passed.TestMethodUniqueID, passed.TestCaseUniqueID, passed.TestUniqueID,
                        passed.ExecutionTime, passed.Output, passed.Warnings, passed.FinishTime);
                    break;
                case ITestFailed failed:
                    _ = Judge($"failed: {failed.ExceptionTypes.FirstOrDefault()}");
                    break;
                case ITestSkipped:
                    _ = Judge("skipped");
                    break;
                case ITestFinished:
                    _index++;
                    break;
            }
            return inner.QueueMessage(message);
        }

        private string? Judge(string outcome)
            => direct is null || CurrentTest.Value is not { } test ? null
                : LiveComparison.Judge(test, direct, _index, outcome,
                    lastCaseOfMethod && _index >= direct.Results.Count - 1);

        public void Dispose() { }
    }
}
