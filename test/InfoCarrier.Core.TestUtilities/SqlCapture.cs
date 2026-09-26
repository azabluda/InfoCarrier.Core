// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.TestUtilities;

/// <summary>What a run does with the statements each test's server ran (#167, <c>docs/sql-capture.md</c> §7, §8).</summary>
public enum SqlCaptureMode
{
    /// <summary>A normal run: each test is compared with its class's <c>.wire.sql</c> entry, if the class has that file.</summary>
    Assert,

    /// <summary>The client is InfoCarrier, and each test writes its entry into <c>&lt;Class&gt;.wire.sql</c>.</summary>
    Wire,

    /// <summary>The client is plain EF Core on the server's store, and each test writes its entry into <c>&lt;Class&gt;.direct.sql</c>.</summary>
    Direct,
}

/// <summary>
///     Ends each test's statements, and in a normal run compares them with the class's capture
///     (#167). Applied to an assembly with the namespaces whose classes are captured.
/// </summary>
/// <remarks>
///     <b>An assembly attribute, which xUnit 2.9.3 honours</b>: <c>XunitTestCaseRunner</c> collects
///     <see cref="BeforeAfterTestAttribute" />s from the test collection, the class, the method and the
///     assembly. <c>After</c> runs before the test class is disposed, so a statement in <c>Dispose</c>
///     belongs to no entry.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class SqlCaptureAttribute(params string[] namespaces) : BeforeAfterTestAttribute
{
    /// <summary>The namespaces whose classes are captured, each with the namespaces below it.</summary>
    public IReadOnlyList<string> Namespaces { get; } = namespaces;

    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest)
    {
        if (CurrentTest.Value is { } test)
        {
            test.Close();
            SqlCapture.After(test);
        }
    }
}

/// <summary>
///     The capture of every test's server SQL (#167): where a class's files live, what a normal run
///     compares, and what a capture run writes.
/// </summary>
/// <remarks>
///     <para>
///         <b>One variable, <see cref="ModeVariable" /></b>: unset is a normal run, which asserts;
///         <c>wire</c> and <c>direct</c> write. Any other value fails every captured test, so a typo
///         never reads as a normal run.
///     </para>
///     <para>
///         <b>A capture writes each class's file when the class's last test case returns</b>, and at
///         process exit whatever is left. It reads the file first, so an entry of a test that did not
///         run stays as it was, and a capture with <c>--filter</c> rewrites only what it ran.
///     </para>
/// </remarks>
public static class SqlCapture
{
    /// <summary>The environment variable that selects <see cref="Mode" />.</summary>
    public const string ModeVariable = "INFOCARRIER_SQL_CAPTURE";

    private static readonly ConcurrentDictionary<Type, string?> Paths = new();
    private static readonly ConcurrentDictionary<string, SqlCaptureFile?> Expected = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<Type, (string Path, SqlCaptureFile File)> Captured = new();
    private static readonly ConcurrentDictionary<(Type, string), int> Ordinals = new();
    private static int _exitHooked;

    /// <summary>What this run does with the statements, from <see cref="ModeVariable" />.</summary>
    /// <exception cref="InvalidOperationException">The variable holds a value other than <c>wire</c> or <c>direct</c>.</exception>
    public static SqlCaptureMode Mode
        => ParseMode(Environment.GetEnvironmentVariable(ModeVariable));

    /// <summary>The mode a value of <see cref="ModeVariable" /> selects.</summary>
    /// <exception cref="InvalidOperationException">A value other than empty, <c>wire</c> or <c>direct</c>.</exception>
    public static SqlCaptureMode ParseMode(string? value)
        => value switch
        {
            null or "" => SqlCaptureMode.Assert,
            "wire" => SqlCaptureMode.Wire,
            "direct" => SqlCaptureMode.Direct,
            _ => throw new InvalidOperationException(
                $"{ModeVariable} is '{value}'. It is unset for a normal run, or 'wire' or 'direct' for a capture."),
        };

    /// <summary>
    ///     The class's <c>.wire.sql</c> or <c>.direct.sql</c> file in the source tree, or null when the
    ///     class is outside the namespaces its assembly's <see cref="SqlCaptureAttribute" /> names.
    /// </summary>
    public static string? FileFor(Type testClass, SqlCaptureMode side)
    {
        ArgumentNullException.ThrowIfNull(testClass);

        return FileFor(
            testClass,
            side,
            OverrideAudit.FindRepositoryDirectory(Path.Combine("test", testClass.Assembly.GetName().Name!)));
    }

    /// <summary>
    ///     The same file under <paramref name="projectFolder" />, the folder of the test project's
    ///     root namespace.
    /// </summary>
    /// <remarks>
    ///     <b>The folder comes from the namespace below the project</b>, which in this project is the
    ///     folder of the class's <c>.cs</c> file, and the name is the class's, with its outer class
    ///     for a nested one: <c>Outer.Inner.wire.sql</c>.
    /// </remarks>
    public static string? FileFor(Type testClass, SqlCaptureMode side, string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(testClass);
        ArgumentNullException.ThrowIfNull(projectFolder);

        string suffix = side switch
        {
            SqlCaptureMode.Wire => "wire",
            SqlCaptureMode.Direct => "direct",
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "A file belongs to the wire side or the direct side."),
        };

        string? space = testClass.Namespace;
        string root = testClass.Assembly.GetName().Name!;
        if (space is null
            || testClass.Assembly.GetCustomAttribute<SqlCaptureAttribute>() is not { } attribute
            || !attribute.Namespaces.Any(n => space == n || space.StartsWith(n + ".", StringComparison.Ordinal)))
        {
            return null;
        }

        if (!space.StartsWith(root + ".", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{testClass.FullName}' is captured, and its namespace is not below its project's, '{root}'.");
        }

        return Path.Combine(
            [projectFolder, .. space[(root.Length + 1)..].Split('.'), $"{FileName(testClass)}.{suffix}.sql"]);
    }

    /// <summary>
    ///     Compares a test's statements with its entry: the text, normalized, and the failure mark,
    ///     never the counts (decision 8).
    /// </summary>
    /// <returns>Null when they agree; otherwise a message that shows both and names the capture command.</returns>
    public static string? Compare(IReadOnlyList<SqlCaptureCommand>? expected, IReadOnlyList<CapturedCommand> actual, string testClassName)
    {
        ArgumentNullException.ThrowIfNull(actual);

        SqlCaptureCommand[] ran = [.. actual.Select(ToFileCommand)];
        string recapture = $"Capture the class again with: eng/sql-capture.sh --filter {testClassName}";
        if (expected is null)
        {
            return $"The capture of {testClassName} has no entry for this test, and the server ran {ran.Length} command(s)."
                + $"\n\nActual:\n{Show(ran)}\n{recapture}";
        }

        int count = Math.Max(expected.Count, ran.Length);
        for (int i = 0; i < count; i++)
        {
            string? what = i >= ran.Length ? "is missing"
                : i >= expected.Count ? "is extra"
                : !Same(ran[i], expected[i]) ? "differs"
                : null;
            if (what is not null)
            {
                return $"The server's SQL differs from the capture of {testClassName}: command #{i + 1} {what}."
                    + $"\n\nExpected:\n{Show(expected)}\nActual:\n{Show(ran)}\n{recapture}";
            }
        }

        return null;
    }

    /// <summary>
    ///     Whether two entries run the same statements: the same text and the same failure marks, in
    ///     the same order. Counts are not compared (decision 8).
    /// </summary>
    /// <remarks>
    ///     The one definition of a difference, for the assertion of a normal run and for the labels'
    ///     compliance test alike.
    /// </remarks>
    public static bool SameStatements(IReadOnlyList<SqlCaptureCommand> x, IReadOnlyList<SqlCaptureCommand> y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        return x.Count == y.Count && x.Zip(y).All(pair => Same(pair.First, pair.Second));
    }

    /// <summary>The test's case in a capture file: its display name without <c>Ns.Class.</c>, and its ordinal.</summary>
    public static SqlCaptureCase CaseOf(CurrentTest test)
    {
        ArgumentNullException.ThrowIfNull(test);

        string prefix = test.TestClass.FullName + ".";
        return new SqlCaptureCase(
            test.DisplayName.StartsWith(prefix, StringComparison.Ordinal) ? test.DisplayName[prefix.Length..] : test.DisplayName,
            test.Ordinal);
    }

    /// <summary>
    ///     The ordinal of the next test of <paramref name="testClass" /> named <paramref name="displayName" />:
    ///     1, then 2, 3, … for cases that xUnit prints alike, in the order they run.
    /// </summary>
    internal static int NextOrdinal(Type testClass, string displayName)
        => Ordinals.AddOrUpdate((testClass, displayName), 1, (_, n) => n + 1);

    /// <summary>The <c>After</c> of every test: the folder check, and in a normal run the comparison.</summary>
    internal static void After(CurrentTest test)
    {
        string? path = Paths.GetOrAdd(test.TestClass, type => FileFor(type, SqlCaptureMode.Wire));
        if (path is null)
        {
            return;
        }

        SqlCaptureMode mode = Mode;

        if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            throw new XunitException(
                $"'{test.TestClass.FullName}' is captured, and the folder its namespace '{test.TestClass.Namespace}' names, "
                + $"'{Path.GetDirectoryName(path)}', does not exist. Move the class, or its namespace, so that the two agree.");
        }

        if (mode == SqlCaptureMode.Assert
            && Expected.GetOrAdd(path, p => File.Exists(p) ? SqlCaptureFile.Read(p) : null) is { } file
            && Compare(file.Find(CaseOf(test)), test.Commands, ClassName(test.TestClass)) is { } difference)
        {
            throw new XunitException($"{difference}\nFile: {path}");
        }
    }

    /// <summary>In a capture, files the test's entry in its class's file, which is written when the class ends.</summary>
    internal static void TestFinished(CurrentTest test, bool failed)
    {
        // An unknown value has already failed the test in After, which is where it can be reported.
        SqlCaptureMode mode;
        try
        {
            mode = Mode;
        }
        catch (InvalidOperationException)
        {
            return;
        }

        if (mode == SqlCaptureMode.Assert
            || FileFor(test.TestClass, mode) is not { } path
            || !Directory.Exists(Path.GetDirectoryName(path)))
        {
            return;
        }

        if (Interlocked.Exchange(ref _exitHooked, 1) == 0)
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                foreach (Type left in Captured.Keys)
                {
                    ClassFinished(left);
                }
            };
        }

        (string _, SqlCaptureFile file) = Captured.GetOrAdd(test.TestClass, _ => (path, SqlCaptureFile.Read(path)));
        lock (file)
        {
            file.Set(CaseOf(test), [.. test.Commands.Select(ToFileCommand)], directRunFailed: mode == SqlCaptureMode.Direct && failed);
        }
    }

    /// <summary>Writes the class's file, when its last test case has returned.</summary>
    internal static void ClassFinished(Type testClass)
    {
        if (Captured.TryRemove(testClass, out (string Path, SqlCaptureFile File) captured))
        {
            lock (captured.File)
            {
                captured.File.Write(captured.Path);
            }
        }
    }

    private static SqlCaptureCommand ToFileCommand(CapturedCommand command)
        => new(
            SqlNormalizer.Normalize(command.Text),
            command.Failed ? "failed"
            : command.Kind switch
            {
                CapturedCommandKind.Reader => command.Count is { } reads ? $"reads {reads}" : "reads ?",
                CapturedCommandKind.NonQuery => $"rows {command.Count}",
                _ => "scalar",
            });

    private static bool Same(SqlCaptureCommand x, SqlCaptureCommand y)
        => x.Text == y.Text && IsFailed(x) == IsFailed(y);

    private static bool IsFailed(SqlCaptureCommand command)
        => command.Outcome == "failed";

    private static string Show(IReadOnlyList<SqlCaptureCommand> commands)
    {
        var text = new StringBuilder();
        for (int i = 0; i < commands.Count; i++)
        {
            text.Append($"-- #{i + 1} {commands[i].Outcome}\n").Append(commands[i].Text).Append('\n');
        }

        return text.ToString();
    }

    // The name xUnit, the TRX and a --filter use: Outer+Inner for a nested class.
    private static string ClassName(Type testClass)
        => testClass.FullName![(testClass.Namespace!.Length + 1)..];

    private static string FileName(Type testClass)
        => testClass.DeclaringType is { } outer ? $"{FileName(outer)}.{testClass.Name}" : testClass.Name;
}
