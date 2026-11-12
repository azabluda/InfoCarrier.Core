// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>
///     The test running in this async flow, for code the test cannot reach: the server's command
///     interceptor, deep inside EF, files each statement under it (#167, ADR-014).
/// </summary>
/// <remarks>
///     <para>
///         Uses xUnit v3's <see cref="TestContext" /> to identify the current test. No custom
///         runner is needed to capture statements in an ordinary run.
///     </para>
///     <para>
///         <b>Null outside a test</b>, and that is part of the contract: a class fixture is built
///         before its class's first test starts, so a shared store's seeding belongs to no test.
///     </para>
/// </remarks>
public sealed class CurrentTest
{
    private static readonly ConditionalWeakTable<IXunitTest, CurrentTest> Tests = new();

    private readonly object _gate = new();
    private readonly List<CapturedCommand> _commands = [];
    private bool _closed;

    private CurrentTest(IXunitTest test)
    {
        DisplayName = test.TestDisplayName;
        TestClass = test.TestCase.TestClass.Class;
        MethodName = test.TestMethod.Method.Name;
    }

    /// <summary>The test running in this async flow, or null outside one.</summary>
    public static CurrentTest? Value => TestContext.Current.Test is IXunitTest test
        ? Tests.GetValue(test, static value => new CurrentTest(value))
        : null;

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
///     <b>An assembly attribute, which xUnit v3 honours</b>: <c>XunitTestCaseRunner</c> collects
///     <see cref="BeforeAfterTestAttribute" />s from the test collection, the class, the method and the
///     assembly.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CloseCurrentTestAttribute : BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest, IXunitTest test)
        => CurrentTest.Value?.Close();
}
