// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class NorthwindKeylessEntitiesTest(NorthwindFixture<NoopModelCustomizer> fixture)
    : NorthwindKeylessEntitiesQueryTestBase<NorthwindFixture<NoopModelCustomizer>>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/NorthwindKeylessEntitiesQueryCosmosTest.cs";

    [StoreLimit(UpstreamRepository.EfCore, Source, 24, 35, Justification = Upstream.GaveNoReason)]
    public override Task KeylessEntity_simple(bool async) => Fixture.NoSyncTest(async, base.KeylessEntity_simple);

    [StoreLimit(UpstreamRepository.EfCore, Source, 37, 48, Justification = Upstream.GaveNoReason)]
    public override Task KeylessEntity_where_simple(bool async) => Fixture.NoSyncTest(async, base.KeylessEntity_where_simple);

    [StoreDefect("1.19", UpstreamRepository.EfCore, Source, 50, 65,
        Case = "async: True", Justification = "Views are not supported.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 50, 65,
        Case = "async: False", Justification = "Always throws for sync.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty.")]
    public override Task KeylessEntity_by_database_view(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (a)
            {
                await Assert.ThrowsAsync<EqualException>(() => base.KeylessEntity_by_database_view(true));
            }
            else
            {
                await base.KeylessEntity_by_database_view(false);
            }
        });

    [StoreLimit(UpstreamRepository.EfCore, Source, 67, 72,
        Justification = Upstream.GaveNoReason)]
    public override async Task Entity_mapped_to_view_on_right_side_of_join(bool async)
    {
        await AssertTranslationFailed(() => base.Entity_mapped_to_view_on_right_side_of_join(async));
    }

    [StoreDefect("1.19", UpstreamRepository.EfCore, Source, 74, 89,
        Case = "async: True", Justification = "Defining queries are not supported.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 74, 89,
        Case = "async: False", Justification = "Always throws for sync.",
        Deviation = DeviationKind.Other,
        DeviationNote = "Execute and assert the synchronous refusal; upstream leaves that arm empty.")]
    public override Task KeylessEntity_with_nav_defining_query(bool async)
        => Fixture.NoSyncTest(async, async a =>
        {
            if (a)
            {
                await Assert.ThrowsAsync<EqualException>(() => base.KeylessEntity_with_nav_defining_query(true));
            }
            else
            {
                await base.KeylessEntity_with_nav_defining_query(false);
            }
        });

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 91, 97,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_with_mixed_tracking(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_with_mixed_tracking(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 99, 105,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_with_included_nav(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_with_included_nav(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 107, 119, Justification = Upstream.GaveNoReason)]
    public override Task KeylessEntity_with_defining_query(bool async) => Fixture.NoSyncTest(async, base.KeylessEntity_with_defining_query);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 121, 127,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_with_defining_query_and_correlated_collection(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_with_defining_query_and_correlated_collection(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 129, 135,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_select_where_navigation(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_select_where_navigation(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 137, 143,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_select_where_navigation_multi_level(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_select_where_navigation_multi_level(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 145, 151,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_with_included_navs_multi_level(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_with_included_navs_multi_level(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 153, 159,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task KeylessEntity_groupby(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.KeylessEntity_groupby(async));
    }

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 161, 167,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override async Task Collection_correlated_with_keyless_entity_in_predicate_works(bool async)
    {
        // Cosmos client evaluation. Issue #17246.
        await AssertTranslationFailed(() => base.Collection_correlated_with_keyless_entity_in_predicate_works(async));
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 169, 179, Justification = Upstream.GaveNoReason)]
    public override Task Auto_initialized_view_set(bool async) => Fixture.NoSyncTest(async, base.Auto_initialized_view_set);

    [StoreLimit(UpstreamRepository.EfCore, Source, 181, 192, Justification = Upstream.GaveNoReason)]
    public override Task Count_over_keyless_entity(bool async) => Fixture.NoSyncTest(async, base.Count_over_keyless_entity);

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 194, 196,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Count_over_keyless_entity_with_pushdown(bool async)
        // Cosmos client evaluation. Issue #17246.
        => AssertTranslationFailed(() => base.Count_over_keyless_entity_with_pushdown(async));

    [StoreIssue(IssueTracker.EfCore, 17246, UpstreamRepository.EfCore, Source, 198, 200,
        Justification = "Cosmos client evaluation. Issue #17246.")]
    public override Task Count_over_keyless_entity_with_pushdown_empty_projection(bool async)
        // Cosmos client evaluation. Issue #17246.
        => AssertTranslationFailed(() => base.Count_over_keyless_entity_with_pushdown_empty_projection(async));

}
