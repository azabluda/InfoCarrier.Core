// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.FunctionalTests.TestUtilities;

namespace InfoCarrier.Core.DocumentStoreTests;

/// <summary>
///     Pins this test assembly to the invariant culture before xUnit starts.
/// </summary>
/// <remarks>
///     The twin of the spec suite's, and one per test assembly is the rule rather than a
///     duplication: a module initializer runs when its own module is loaded, so one in the shared
///     harness would run on first use of that library and not reliably before these test threads.
///     <c>CA2255</c> says so. Why the culture is pinned at all, and the nine failures it removed
///     from the spec suite, is on <see cref="InvariantCulture" />. Nothing in this tier is known to
///     depend on it today; it is here so that a figure from this tier means the same on any
///     machine, which is the same reason the spec suite has one.
/// </remarks>
internal static class InvariantCultureInitializer
{
    [ModuleInitializer]
    internal static void PinInvariantCulture()
        => InvariantCulture.Pin();
}
