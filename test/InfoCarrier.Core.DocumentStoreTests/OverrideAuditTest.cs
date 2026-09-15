// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Every override of a specification test in this tier says what the store does and where that
///     is shown.
/// </summary>
/// <remarks>
///     <b>The report is the audit.</b> It lists every override with its label, its reference, whether
///     it skips, and why it deviates from upstream, so "how much does this tier not check, and why"
///     has a written answer on every run. <c>docs/plans/v10/test-overhaul.md</c> is the reading.
/// </remarks>
public class OverrideAuditTest(ITestOutputHelper output)
{
    [Fact]
    public void Every_override_says_what_the_store_does_and_where_that_is_shown()
    {
        OverrideAuditResult audit = OverrideAudit.Run(
            typeof(OverrideAuditTest).Assembly,
            OverrideAudit.FindRepositoryFile("docs/upstream-defects.md"));

        output.WriteLine(audit.Report);
        foreach (string violation in audit.Violations)
        {
            output.WriteLine(violation);
        }

        Assert.Empty(audit.Violations);
    }
}
