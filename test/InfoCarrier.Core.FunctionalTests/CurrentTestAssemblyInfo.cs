// Licensed under the MIT license. See license.txt file in the project root for license information.

using Xunit;

// Every test runs with CurrentTest set, so the server's statements can be filed under the test that
// caused them (#167). See CurrentTestFramework.
[assembly: TestFramework(
    "InfoCarrier.Core.FunctionalTests.TestUtilities.CurrentTestFramework",
    "InfoCarrier.Core.TestUtilities")]
