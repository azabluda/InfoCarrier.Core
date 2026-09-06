// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.FunctionalTests.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests;

/// <summary>
///     Pins this test assembly to the invariant culture before xUnit starts.
/// </summary>
/// <remarks>
///     <b>One of these per test assembly, and it cannot live in the shared harness.</b> A module
///     initializer runs when its own module is loaded, and a library's module is loaded on first
///     use, which is not guaranteed to precede the test threads of the assembly that referenced it.
///     <c>CA2255</c> says so and CI treats it as an error. The reasoning for pinning at all, and
///     the nine failures it removes, is on <see cref="InvariantCulture" />.
/// </remarks>
internal static class InvariantCultureInitializer
{
    [ModuleInitializer]
    internal static void PinInvariantCulture()
        => InvariantCulture.Pin();
}
