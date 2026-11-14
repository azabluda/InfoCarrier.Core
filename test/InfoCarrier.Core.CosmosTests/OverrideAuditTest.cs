// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

public class OverrideAuditTest(ITestOutputHelper output)
{
    [Fact]
    public void Every_override_names_its_store_evidence()
    {
        OverrideAuditResult audit = OverrideAudit.Run(typeof(OverrideAuditTest).Assembly,
            OverrideAudit.FindRepositoryFile("docs/upstream-defects.md"),
            OverrideAudit.FindRepositoryFile("docs/decisions.md"));
        output.WriteLine(audit.Report);
        Assert.Empty(audit.Violations);
    }
}
