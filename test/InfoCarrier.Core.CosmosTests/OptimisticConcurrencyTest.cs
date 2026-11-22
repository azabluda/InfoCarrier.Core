// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests;

public class OptimisticConcurrencyTest(CosmosConcurrencyFixture fixture)
    : OptimisticConcurrencyTestBase<CosmosConcurrencyFixture, byte[]>(fixture), IAsyncLifetime
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/OptimisticConcurrencyCosmosTest.cs";

    [StoreIssue(IssueTracker.EfCore, 17670, UpstreamRepository.EfCore, Source, 11, 14, Skip = true, Justification = "Non-persisted property in query")]
    public override Task Calling_GetDatabaseValues_on_owned_entity_works(bool async) => throw SkipException.ForSkip("Non-persisted property in query");

    [StoreLimit(UpstreamRepository.EfCore, Source, 16, 17, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_owned_entity_works(bool async) => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreLimit(UpstreamRepository.EfCore, Source, 19, 21, Skip = true, Justification = "Only ETag properties can be used as concurrency tokens")]
    public override Task Concurrency_issue_where_the_FK_is_the_concurrency_token_can_be_handled() => throw SkipException.ForSkip("Only ETag properties can be used as concurrency tokens");

    [StoreLimit(UpstreamRepository.EfCore, Source, 23, 25, Skip = true, Justification = Upstream.GaveNoReason)]
    public override void Nullable_client_side_concurrency_token_can_be_used() => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreDefect("1.22", UpstreamRepository.EfCore, Source, 27, 29, Skip = true, Justification = "ETag concurrency doesn't work after an item was deleted")]
    public override Task Deleting_the_same_entity_twice_results_in_DbUpdateConcurrencyException() => throw SkipException.ForSkip("ETag concurrency doesn't work after an item was deleted");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 31, 37, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_client_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 39, 45, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 47, 53, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_new_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 55, 61, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values_using_equivalent_of_accept_changes() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 63, 69, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Simple_concurrency_exception_can_be_resolved_with_store_values_using_Reload() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("1.22", UpstreamRepository.EfCore, Source, 71, 72, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Deleting_then_updating_the_same_entity_results_in_DbUpdateConcurrencyException() => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreDefect("1.22", UpstreamRepository.EfCore, Source, 74, 76, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Deleting_then_updating_the_same_entity_results_in_DbUpdateConcurrencyException_which_can_be_resolved_with_store_values() => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 78, 84, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 86, 94, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Updating_then_deleting_the_same_entity_results_in_DbUpdateConcurrencyException_which_can_be_resolved_with_store_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 96, 97, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Attempting_to_delete_same_relationship_twice_for_many_to_many_results_in_independent_association_exception() => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreLimit(UpstreamRepository.EfCore, Source, 99, 100, Skip = true, Justification = Upstream.GaveNoReason)]
    public override Task Attempting_to_add_same_relationship_twice_for_many_to_many_results_in_independent_association_exception() => throw SkipException.ForSkip(Upstream.GaveNoReason);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 102, 111, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Two_concurrency_issues_in_one_to_one_related_entities_can_be_handled_by_dealing_with_dependent_first() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 113, 122, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Two_concurrency_issues_in_one_to_many_related_entities_can_be_handled_by_dealing_with_dependent_first() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 127, 128, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_an_Added_entity_that_is_not_in_database_is_no_op(bool async)
        => Fixture.NoSyncTest(async, base.Calling_Reload_on_an_Added_entity_that_is_not_in_database_is_no_op);

    [StoreLimit(UpstreamRepository.EfCore, Source, 130, 132, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_an_Unchanged_entity_that_is_not_in_database_detaches_it(bool async)
        => Fixture.NoSyncTest(
            async, base.Calling_Reload_on_an_Unchanged_entity_that_is_not_in_database_detaches_it);

    [StoreLimit(UpstreamRepository.EfCore, Source, 134, 136, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_a_Modified_entity_that_is_not_in_database_detaches_it(bool async)
        => Fixture.NoSyncTest(
            async, base.Calling_Reload_on_a_Modified_entity_that_is_not_in_database_detaches_it);

    [StoreLimit(UpstreamRepository.EfCore, Source, 138, 140, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_a_Deleted_entity_that_is_not_in_database_detaches_it(bool async)
        => Fixture.NoSyncTest(
            async, base.Calling_Reload_on_a_Deleted_entity_that_is_not_in_database_detaches_it);

    [StoreLimit(UpstreamRepository.EfCore, Source, 142, 144, Justification = Upstream.GaveNoReason)]
    public override Task Calling_Reload_on_a_Detached_entity_that_is_not_in_database_detaches_it(bool async)
        => Fixture.NoSyncTest(
            async, base.Calling_Reload_on_a_Detached_entity_that_is_not_in_database_detaches_it);

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 146, 152, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Calling_Reload_on_an_Unchanged_entity_makes_the_entity_unchanged(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 154, 160, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Calling_Reload_on_a_Modified_entity_makes_the_entity_unchanged(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 162, 168, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Calling_Reload_on_a_Deleted_entity_makes_the_entity_unchanged(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 170, 177, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Calling_Reload_on_an_Added_entity_that_was_saved_elsewhere_makes_the_entity_unchanged(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 179, 185, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Calling_Reload_on_a_Detached_entity_makes_the_entity_unchanged(bool async) => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    protected override IDbContextTransaction BeginTransaction(DatabaseFacade facade) => new FakeDbContextTransaction();

    public ValueTask InitializeAsync() => new(Fixture.ReseedAsync());
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private class FakeDbContextTransaction : IDbContextTransaction
    {
        public Guid TransactionId
            => new();

        public void Commit()
        {
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => default;

        public void Rollback()
        {
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

}
