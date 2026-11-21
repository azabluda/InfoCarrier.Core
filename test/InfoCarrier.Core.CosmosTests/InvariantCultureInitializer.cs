// Licensed under the MIT license. See license.txt file in the project root for license information.

using System.Runtime.CompilerServices;
using InfoCarrier.Core.FunctionalTests.TestUtilities;

namespace InfoCarrier.Core.CosmosTests;

internal static class InvariantCultureInitializer
{
    [ModuleInitializer]
    internal static void PinInvariantCulture()
        => InvariantCulture.Pin();
}
