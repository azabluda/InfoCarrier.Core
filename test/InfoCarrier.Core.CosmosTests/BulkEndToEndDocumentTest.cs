// Licensed under the MIT license. See license.txt file in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Specifications;

// Pinned official Update/CosmosBulkEndToEndTest backend options.
public class BulkEndToEndDocumentTest(NonSharedFixture fixture) : EndToEndDocumentTest(fixture)
{
    protected override DbContextOptionsBuilder ConfigureBackendOptions(DbContextOptionsBuilder builder)
        => base.ConfigureBackendOptions(builder).UseCosmos(o => o.BulkExecutionAllowed())
            .ConfigureWarnings(w => w.Ignore(CosmosEventId.BulkExecutionWithTransactionalBatch));
}

// Pinned official Update/CosmosBulkEndToEndTestNoBatching and its exact true-arm exclusion.
public class BulkEndToEndDocumentNoBatchingTest(NonSharedFixture fixture) : EndToEndDocumentTest(fixture)
{
    protected override AutoTransactionBehavior BackendTransactionMode => AutoTransactionBehavior.Never;
    protected override DbContextOptionsBuilder ConfigureBackendOptions(DbContextOptionsBuilder builder)
        => base.ConfigureBackendOptions(builder).UseCosmos(o => o.BulkExecutionAllowed());

    protected override TContext CreateContext<TContext>(ContextFactory<TContext> factory, bool transactionalBatch)
    {
        var context = base.CreateContext(factory, transactionalBatch);
        context.Database.AutoTransactionBehavior = !transactionalBatch
            ? AutoTransactionBehavior.Never
            : throw SkipException.ForSkip("Only AutoTransactionBehavior.Never is tested.");
        return context;
    }
}
