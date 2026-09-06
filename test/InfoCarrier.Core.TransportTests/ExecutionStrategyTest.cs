// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Northwind.Shared;
using Xunit;

namespace InfoCarrier.Core.TransportTests;

public class ExecutionStrategyTest
{
    [Fact]
    public void The_shipped_execution_strategy_does_not_retry()
    {
        // PINS WHAT THE DOCUMENTATION SAYS. `transactions.md` shows the
        // `CreateExecutionStrategy().ExecuteAsync(...)` pattern, and a reader of that page asked
        // whether the strategy it returns retries anything. It does not: this provider registers
        // no `IExecutionStrategyFactory`, so EF's default answers, and EF's default is
        // `NonRetryingExecutionStrategy`. The snippet is still right, because a retrying strategy
        // a caller configures runs through the same shape, and the page now says so rather than
        // leaving a reader to assume the wrapper is doing something.
        //
        // Asserted rather than read out of EF's source, so that a change in EF's default fails
        // here instead of quietly making the page wrong.
        using var context = new NorthwindContext(
            new DbContextOptionsBuilder<NorthwindContext>()
                .UseInfoCarrier(new TransportInfoCarrierClient(
                    new UnreachableTransport(), new SystemTextJsonInfoCarrierSerializer()))
                .Options);

        IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

        Assert.False(strategy.RetriesOnFailure);
    }

    private sealed class UnreachableTransport : IInfoCarrierTransport
    {
        public Task<InfoCarrierEnvelope> SendAsync(
            InfoCarrierEnvelope request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException(
                "Creating an execution strategy must not reach the server.");
    }
}
