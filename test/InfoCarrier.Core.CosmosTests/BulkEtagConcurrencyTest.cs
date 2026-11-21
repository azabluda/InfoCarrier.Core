// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfoCarrier.Core.CosmosTests.Specifications;

// Pinned official CosmosBulkConcurrencyTest applies bulk execution and Never on every context.
public class BulkEtagConcurrencyTest : EtagConcurrencyTest
{
    protected override DbContextOptionsBuilder ConfigureServerOptions(DbContextOptionsBuilder builder)
        => base.ConfigureServerOptions(builder).UseCosmos(o => o.BulkExecutionAllowed())
            .AddInterceptors(new NeverBatchInterceptor());

    protected override ConcurrencyContext CreateContext(DbContextOptions options)
    {
        var context = base.CreateContext(options);
        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        return context;
    }

    // Transaction mode is explicit backend fixture configuration; it is not a wire feature.
    private sealed class NeverBatchInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            eventData.Context!.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            eventData.Context!.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
            return ValueTask.FromResult(result);
        }
    }
}
