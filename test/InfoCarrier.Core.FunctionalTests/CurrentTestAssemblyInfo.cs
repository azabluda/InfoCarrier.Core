// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Xunit;

// Every test runs with CurrentTest set, so the server's statements can be filed under the test that
// caused them, and After closes it before the test class is disposed (#167, ADR-014). See
// CurrentTestFramework and CloseCurrentTestAttribute.
[assembly: TestFramework(
    "InfoCarrier.Core.FunctionalTests.TestUtilities.CurrentTestFramework",
    "InfoCarrier.Core.TestUtilities")]
[assembly: CloseCurrentTest]
