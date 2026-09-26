// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Reflection;
using System.Text.RegularExpressions;
using InfoCarrier.Core.FunctionalTests.Sqlite;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     The labels of the SQL capture and its files agree (#167, <c>docs/sql-capture.md</c> §9):
///     every <see cref="DeviationKind.SqlDiffers" /> reason covers a real difference, and every entry
///     names a test of its class.
/// </summary>
/// <remarks>
///     <para>
///         <b>Each check is a function of the classes and a project folder</b>, so that a pin can run
///         it on files it writes into a folder of its own, against the abstract classes in
///         <c>Sqlite/SqlCaptureComplianceFixtures.cs</c>. Over the real repository they read the
///         classes xUnit runs.
///     </para>
///     <para>
///         <b>A difference is a statement's text or its failure mark, never a count</b>, as the
///         assertion of a normal run compares (decision 8). A case the <c>.direct.sql</c> file lacks
///         shows no difference, because a label needs both sides to be checked.
///     </para>
/// </remarks>
public sealed class SqlCaptureComplianceTest : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), nameof(SqlCaptureComplianceTest), Guid.NewGuid().ToString("N"));

    public SqlCaptureComplianceTest()
        => Directory.CreateDirectory(Path.Combine(_folder, "Sqlite"));

    /// <summary>The classes xUnit runs: not abstract, and with a test method.</summary>
    private static IEnumerable<Type> TestClasses
        => typeof(SqlCaptureComplianceTest).Assembly.GetTypes()
            .Where(t => t.IsClass
                && !t.IsAbstract
                && t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Any(m => m.IsDefined(typeof(FactAttribute), inherit: true)));

    private static string ProjectFolder
        => OverrideAudit.FindRepositoryDirectory(Path.Combine("test", typeof(SqlCaptureComplianceTest).Assembly.GetName().Name!));

    public void Dispose()
        => Directory.Delete(_folder, recursive: true);

    [ConditionalFact]
    public void Every_SqlDiffers_reason_covers_a_difference_between_the_two_files()
        => Assert.Empty(LabelViolations(TestClasses, ProjectFolder));

    [ConditionalFact]
    public void Every_entry_names_a_test_of_its_class()
        => Assert.Empty(EntryViolations(TestClasses, ProjectFolder));

    [ConditionalFact]
    public void An_entry_naming_a_method_the_class_lacks_is_a_violation()
    {
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Wire, "-- NoSuchMethod\n-- Plain\n-- #1 reads 2\nSELECT 1\n");

        Assert.Contains(
            "'NoSuchMethod'",
            Assert.Single(EntryViolations([typeof(SqlCaptureComplianceFixture)], _folder)),
            StringComparison.Ordinal);
    }

    [ConditionalFact]
    public void A_file_of_no_class_is_a_violation()
    {
        File.WriteAllText(Path.Combine(_folder, "Sqlite", "Gone.direct.sql"), "-- Plain\n");

        Assert.Contains(
            "Gone.direct.sql",
            Assert.Single(EntryViolations([typeof(SqlCaptureComplianceFixture)], _folder)),
            StringComparison.Ordinal);
    }

    [ConditionalFact]
    public void A_SqlDiffers_reason_on_a_class_with_no_file_is_a_violation()
        => Assert.Contains(
            "SqlCaptureComplianceFixture.Differs",
            Assert.Single(LabelViolations([typeof(SqlCaptureComplianceFixture)], _folder)),
            StringComparison.Ordinal);

    [ConditionalFact]
    public void A_SqlDiffers_reason_whose_case_is_the_same_on_both_sides_is_a_violation()
    {
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Wire, "-- Differs\n-- #1 reads 2\nSELECT 1\n");
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Direct, "-- Differs\n-- #1 reads 2\nSELECT 1\n");

        Assert.Contains(
            "no case it covers differs",
            Assert.Single(LabelViolations([typeof(SqlCaptureComplianceFixture)], _folder)),
            StringComparison.Ordinal);
    }

    [ConditionalFact]
    public void A_SqlDiffers_reason_whose_case_differs_is_no_violation()
    {
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Wire, "-- Differs\n-- #1 reads 2\nSELECT 1\n");
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Direct, "-- Differs\n-- #1 reads 2\nSELECT 2\n");

        Assert.Empty(LabelViolations([typeof(SqlCaptureComplianceFixture)], _folder));
    }

    /// <summary>A count is recorded and never compared (decision 8), so a count alone is no difference.</summary>
    [ConditionalFact]
    public void A_count_alone_is_no_difference()
    {
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Wire, "-- Differs\n-- #1 reads 2\nSELECT 1\n");
        Write(typeof(SqlCaptureComplianceFixture), SqlCaptureMode.Direct, "-- Differs\n-- #1 reads 3\nSELECT 1\n");

        Assert.Single(LabelViolations([typeof(SqlCaptureComplianceFixture)], _folder));
    }

    [ConditionalFact]
    public void A_reason_covers_only_the_cases_its_Case_names()
    {
        const string wire = "-- Differs_when_async(async: False)\n-- Differs_when_async(async: True)\n-- #1 reads 2\nSELECT 1\n";
        Write(typeof(SqlCaptureComplianceCaseFixture), SqlCaptureMode.Wire, wire);
        Write(
            typeof(SqlCaptureComplianceCaseFixture),
            SqlCaptureMode.Direct,
            "-- Differs_when_async(async: False)\n-- #1 reads 2\nSELECT 2\n\n-- Differs_when_async(async: True)\n-- #1 reads 2\nSELECT 1\n");
        Assert.Single(LabelViolations([typeof(SqlCaptureComplianceCaseFixture)], _folder));

        Write(
            typeof(SqlCaptureComplianceCaseFixture),
            SqlCaptureMode.Direct,
            "-- Differs_when_async(async: False)\n-- #1 reads 2\nSELECT 1\n\n-- Differs_when_async(async: True)\n-- #1 reads 2\nSELECT 2\n");
        Assert.Empty(LabelViolations([typeof(SqlCaptureComplianceCaseFixture)], _folder));
    }

    /// <summary>
    ///     Every InfoCarrier reason with <see cref="DeviationKind.SqlDiffers" /> names a method whose
    ///     class has both files, and one of whose cases, narrowed by the reason's <c>Case</c>, differs
    ///     between them.
    /// </summary>
    /// <remarks>
    ///     When a fix removes a difference, this fails until the reason goes, and a pass-through
    ///     override with it.
    /// </remarks>
    public static IReadOnlyList<string> LabelViolations(IEnumerable<Type> classes, string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(classes);

        var violations = new List<string>();
        foreach (Type type in classes.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                foreach (OverrideReasonAttribute reason in method.GetCustomAttributes<OverrideReasonAttribute>(inherit: false)
                    .Where(r => r is InfoCarrierDesignAttribute or InfoCarrierDefectAttribute
                        && (r.Deviation & DeviationKind.SqlDiffers) != DeviationKind.None))
                {
                    string at = reason.Case is null ? $"{type.Name}.{method.Name}" : $"{type.Name}.{method.Name} [{reason.Case}]";
                    string? wirePath = SqlCapture.FileFor(type, SqlCaptureMode.Wire, projectFolder);
                    string? directPath = SqlCapture.FileFor(type, SqlCaptureMode.Direct, projectFolder);
                    if (wirePath is null || directPath is null)
                    {
                        violations.Add($"{at}: carries SqlDiffers, and its class is not captured, so its SQL is compared with nothing.");
                        continue;
                    }

                    if (!File.Exists(wirePath) || !File.Exists(directPath))
                    {
                        violations.Add($"{at}: carries SqlDiffers, and its class has no {Path.GetFileName(wirePath)} or no {Path.GetFileName(directPath)}.");
                        continue;
                    }

                    SqlCaptureFile wire = SqlCaptureFile.Read(wirePath);
                    SqlCaptureFile direct = SqlCaptureFile.Read(directPath);
                    bool differs = wire.Cases
                        .Where(c => MethodOf(c) == method.Name && Covers(reason.Case, c.Name))
                        .Any(c => direct.Find(c) is { } reference && !SqlCapture.SameStatements(wire.Find(c)!, reference));
                    if (!differs)
                    {
                        violations.Add(
                            $"{at}: carries SqlDiffers, and no case it covers differs between {Path.GetFileName(wirePath)} "
                            + $"and {Path.GetFileName(directPath)}. Delete the reason, and a pass-through override with it.");
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    ///     Every entry of every <c>.wire.sql</c> and <c>.direct.sql</c> file names a test method of its
    ///     class, inherited ones included, and every such file belongs to a class.
    /// </summary>
    /// <remarks>
    ///     An entry left behind by a test that EF renamed or deleted fails this, as a base that EF
    ///     adds fails <c>InfoCarrierComplianceTest</c>.
    /// </remarks>
    public static IReadOnlyList<string> EntryViolations(IEnumerable<Type> classes, string projectFolder)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(projectFolder);

        var violations = new List<string>();
        var owned = new HashSet<string>(StringComparer.Ordinal);
        var folders = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Type type in classes.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            if (type.Assembly.GetCustomAttribute<SqlCaptureAttribute>() is { } attribute)
            {
                string root = type.Assembly.GetName().Name!;
                folders.UnionWith(attribute.Namespaces
                    .Where(n => n.StartsWith(root + ".", StringComparison.Ordinal))
                    .Select(n => Path.Combine([projectFolder, .. n[(root.Length + 1)..].Split('.')])));
            }

            HashSet<string>? tests = null;
            foreach (SqlCaptureMode side in new[] { SqlCaptureMode.Wire, SqlCaptureMode.Direct })
            {
                if (SqlCapture.FileFor(type, side, projectFolder) is not { } path)
                {
                    continue;
                }

                owned.Add(path);
                if (!File.Exists(path))
                {
                    continue;
                }

                tests ??= [.. type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.IsDefined(typeof(FactAttribute), inherit: true))
                    .Select(m => m.Name)];
                foreach (SqlCaptureCase testCase in SqlCaptureFile.Read(path).Cases
                    .OrderBy(c => c.Name, StringComparer.Ordinal)
                    .ThenBy(c => c.Ordinal))
                {
                    if (!tests.Contains(MethodOf(testCase)))
                    {
                        violations.Add($"{Path.GetFileName(path)}: '{testCase.Name}' names no test method of {type.Name}.");
                    }
                }
            }
        }

        foreach (string file in folders
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.sql", SearchOption.AllDirectories))
            .Where(f => f.EndsWith(".wire.sql", StringComparison.Ordinal) || f.EndsWith(".direct.sql", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            if (!owned.Contains(file))
            {
                violations.Add($"{Path.GetRelativePath(projectFolder, file)}: belongs to no test class. Delete it, or move it beside its class.");
            }
        }

        return violations;
    }

    /// <summary>The test method a case names: its name up to the arguments.</summary>
    private static string MethodOf(SqlCaptureCase testCase)
        => testCase.Name.IndexOf('(', StringComparison.Ordinal) is var open and >= 0 ? testCase.Name[..open] : testCase.Name;

    /// <summary>Whether a reason's <c>Case</c> covers a case, as <c>eng/spec-parity.py</c> decides it.</summary>
    private static bool Covers(string? reasonCase, string name)
        => reasonCase is null
            || (name.IndexOf('(', StringComparison.Ordinal) is var open and >= 0
                && Regex.IsMatch(name[open..], $@"\b{Regex.Escape(reasonCase)}\b"));

    private void Write(Type testClass, SqlCaptureMode side, string text)
        => File.WriteAllText(SqlCapture.FileFor(testClass, side, _folder)!, text);
}
