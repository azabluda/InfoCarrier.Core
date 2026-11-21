// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Azure.Cosmos;
using Xunit;

namespace InfoCarrier.Core.CosmosTests;

/// <summary>Asserts the actual exception type on each comparison path, then exposes a common assertion view.</summary>
internal static class CosmosExceptionAssert
{
    public static async Task<InfoCarrierServerException> ThrowsAsync(Func<Task> action)
    {
        if (!DirectClient.IsEnabled) return await Assert.ThrowsAsync<InfoCarrierServerException>(action);
        var error = await Assert.ThrowsAsync<CosmosException>(action);
        return new InfoCarrierServerException(error.GetType().FullName!, error.Message, error);
    }

    public static InfoCarrierServerException Throws(Action action)
    {
        if (!DirectClient.IsEnabled) return Assert.Throws<InfoCarrierServerException>(action);
        var error = Assert.Throws<CosmosException>(action);
        return new InfoCarrierServerException(error.GetType().FullName!, error.Message, error);
    }
}
