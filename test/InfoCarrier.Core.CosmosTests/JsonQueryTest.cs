// Licensed under the MIT license. See license.txt file in the project root for license information.

// Adapted from pinned EF Core 11 JsonQueryTest; SQL baselines are omitted.

using System.Text;
using System.Net;
using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore.Cosmos.Internal;
using Microsoft.EntityFrameworkCore.Cosmos.Storage.Internal;
using Microsoft.EntityFrameworkCore.TestModels.JsonQuery;

namespace InfoCarrier.Core.CosmosTests.Specifications;

#pragma warning disable EF1001 // Preserve pinned Cosmos diagnostic resources and malformed-document setup.
public class JsonQueryTest : JsonQueryTestBase<JsonQueryFixture>
{
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports both primitive arrays through a positional carrier.",
        DeviationNote = "SELECT VALUE uses Item1/Item2 for IntArray/ListOfString; array expressions and row ordering match the direct provider.")]
    public override Task Project_json_array_of_primitives_on_reference()
        => base.Project_json_array_of_primitives_on_reference();

    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/JsonQueryCosmosTest.cs";
    public JsonQueryTest(JsonQueryFixture fixture) : base(fixture) { }

    private readonly NonSharedModelInfoCarrierHarness _nonShared = new(new CosmosTier());

    protected override ITestStoreFactory NonSharedTestStoreFactory => _nonShared.TestStoreFactory;

    protected override ContextFactory<TContext> CreateContextFactory<TContext>(
        Action<ModelBuilder>? onModelCreating = null,
        Action<DbContextOptionsBuilder>? onConfiguring = null,
        Func<Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.DependencyInjection.IServiceCollection>? addServices = null,
        Action<ModelConfigurationBuilder>? configureConventions = null,
        Func<string, bool>? shouldLogCategory = null,
        Func<TestStore>? createTestStore = null,
        bool usePooling = true,
        bool useServiceProvider = true,
        bool skipValidation = false)
    {
        NonSharedFixture = null;
        _nonShared.Prepare(typeof(TContext), onModelCreating, addServices, onConfiguring, configureConventions, AddNonSharedOptions);
        return base.CreateContextFactory<TContext>(
            onModelCreating, onConfiguring, addServices, configureConventions,
            shouldLogCategory, createTestStore, usePooling, useServiceProvider, skipValidation);
    }


    private static async Task CreateCustomEntityHelperAsync(
        Container container,
        string json,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var response = await container.CreateItemStreamAsync(
                stream,
                PartitionKey.None,
                requestOptions: null,
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"Failed to create entity (status code: {response.StatusCode}) for json: {json}");
        }
    }

    private static void SkipOnLinuxEmulator() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");
    private static void SkipOnCI()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            throw SkipException.ForSkip("Test does not run on CI");
    }
    private static async Task<InfoCarrierServerException> AssertCosmosExceptionAsync(Func<Task> action)
    {
        var error = await CosmosExceptionAssert.ThrowsAsync(action);
        Assert.Equal("Microsoft.Azure.Cosmos.CosmosException", error.ServerExceptionTypeName);
        return error;
    }

    private const string NotImplementedBindPropertyMessage
        = "Bind property on structural type coming out of scalar subquery";



    [StoreLimit(UpstreamRepository.EfCore, Source, 25, 37, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_enum_inside_json_entity(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_enum_inside_json_entity(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 39, 63, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Basic_json_projection_owned_collection_branch_NoTrackingWithIdentityResolution(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            await AssertQuery(
                async,
                ss => ss.Set<JsonEntityBasic>()
                    .Select(x => new { x.Id, x.OwnedReferenceRoot.OwnedCollectionBranch })
                    .AsNoTrackingWithIdentityResolution(),
                elementSorter: e => e.Id,
                elementAsserter: (e, a) =>
                {
                    AssertEqual(e.Id, a.Id);
                    AssertCollection(e.OwnedCollectionBranch, a.OwnedCollectionBranch, ordered: true);
                });


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 65, 79, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Basic_json_projection_owned_collection_leaf(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            await base.Basic_json_projection_owned_collection_leaf(async);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 81, 102, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_owned_collection_root_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async asyncQuery =>
            {
                await AssertQuery(
                    asyncQuery,
                    ss => ss.Set<JsonEntityBasic>().Select(x => new { x.Id, x.OwnedCollectionRoot })
                        .AsNoTrackingWithIdentityResolution(),
                    elementSorter: e => e.Id,
                    elementAsserter: (e, a) =>
                    {
                        AssertEqual(e.Id, a.Id);
                        AssertCollection(e.OwnedCollectionRoot, a.OwnedCollectionRoot, ordered: true);
                    });


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 104, 126, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Basic_json_projection_owned_reference_branch_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async asyncQuery =>
            {
                await AssertQuery(
                    asyncQuery,
                    ss => ss.Set<JsonEntityBasic>()
                        .Select(x => new { x.Id, x.OwnedReferenceRoot.OwnedReferenceBranch })
                        .AsNoTrackingWithIdentityResolution(),
                    elementSorter: e => e.Id,
                    elementAsserter: (e, a) =>
                    {
                        AssertEqual(e.Id, a.Id);
                        AssertEqual(e.OwnedReferenceBranch, a.OwnedReferenceBranch);
                    });


            });

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 128, 171, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Basic_json_projection_owned_reference_duplicated2_NoTrackingWithIdentityResolution(bool async)
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        return Fixture.NoSyncTest(
            async, async asyncQuery =>
            {
                await AssertQuery(
                    asyncQuery,
                    ss => ss.Set<JsonEntityBasic>()
                        .OrderBy(x => x.Id)
                        .Select(x => new
                        {
                            x.Id,
                            Root1 = x.OwnedReferenceRoot,
                            Leaf1 = x.OwnedReferenceRoot!.OwnedReferenceBranch!.OwnedReferenceLeaf,
                            Root2 = x.OwnedReferenceRoot,
                            Leaf2 = x.OwnedReferenceRoot!.OwnedReferenceBranch!.OwnedReferenceLeaf,
                        }).AsNoTrackingWithIdentityResolution(),
                    assertOrder: true,
                    elementAsserter: (e, a) =>
                    {
                        AssertEqual(e.Id, a.Id);
                        AssertEqual(e.Root1, a.Root1);
                        AssertEqual(e.Root2, a.Root2);
                        AssertEqual(e.Leaf1, a.Leaf1);
                        AssertEqual(e.Leaf2, a.Leaf2);
                    });


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 173, 211, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Basic_json_projection_owned_reference_duplicated_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async asyncQuery =>
            {
                await AssertQuery(
                    asyncQuery,
                    ss => ss.Set<JsonEntityBasic>()
                        .OrderBy(x => x.Id)
                        .Select(x => new
                        {
                            x.Id,
                            Root1 = x.OwnedReferenceRoot,
                            Branch1 = x.OwnedReferenceRoot.OwnedReferenceBranch,
                            Root2 = x.OwnedReferenceRoot,
                            Branch2 = x.OwnedReferenceRoot.OwnedReferenceBranch,
                        }).AsNoTrackingWithIdentityResolution(),
                    assertOrder: true,
                    elementAsserter: (e, a) =>
                    {
                        AssertEqual(e.Id, a.Id);
                        AssertEqual(e.Root1, a.Root1);
                        AssertEqual(e.Root2, a.Root2);
                        AssertEqual(e.Branch1, a.Branch1);
                        AssertEqual(e.Branch2, a.Branch2);
                    });


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 213, 225, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_owned_reference_leaf(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_owned_reference_leaf(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 227, 248, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Basic_json_projection_owned_reference_root_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async asyncQuery =>
            {
                await AssertQuery(
                    asyncQuery,
                    ss => ss.Set<JsonEntityBasic>().Select(x => new { x.OwnedReferenceRoot, x.Id })
                        .AsNoTrackingWithIdentityResolution(),
                    elementSorter: e => e.Id,
                    elementAsserter: (e, a) =>
                    {
                        AssertEqual(e.Id, a.Id);
                        AssertEqual(e.OwnedReferenceRoot, a.OwnedReferenceRoot);
                    });


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 250, 262, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_owner_entity_duplicated_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_owner_entity_duplicated_NoTrackingWithIdentityResolution(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 264, 276, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_owner_entity_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_owner_entity_NoTrackingWithIdentityResolution(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 278, 290, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_owner_entity_twice_NoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_owner_entity_twice_NoTrackingWithIdentityResolution(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 292, 304, Justification = Upstream.GaveNoReason)]
    public override Task Basic_json_projection_scalar(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Basic_json_projection_scalar(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 306, 314, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Custom_naming_projection_everything(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Custom_naming_projection_everything(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 316, 329, Justification = Upstream.GaveNoReason)]
    public override Task Custom_naming_projection_owned_collection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Custom_naming_projection_owned_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 331, 343, Justification = Upstream.GaveNoReason)]
    public override Task Custom_naming_projection_owned_reference(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Custom_naming_projection_owned_reference(async);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 345, 357, Justification = Upstream.GaveNoReason)]
    public override Task Custom_naming_projection_owned_scalar(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Custom_naming_projection_owned_scalar(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 359, 371, Justification = Upstream.GaveNoReason)]
    public override Task Custom_naming_projection_owner_entity(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Custom_naming_projection_owner_entity(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 373, 382, Justification = Upstream.GaveNoReason)]
    public override async Task Entity_including_collection_with_json(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Entity_including_collection_with_json(async)))
            .Message;

        Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: EntityBasic.JsonEntityBasics (List<JsonEntityBasic>) Collection ToDependent JsonEntityBasic"),
            message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 384, 393, Justification = Upstream.GaveNoReason)]
    public override async Task Entity_including_collection_with_json_AsNoTrackingWithIdentityResolution(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Entity_including_collection_with_json_AsNoTrackingWithIdentityResolution(async))).Message;

        Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: EntityBasic.JsonEntityBasics (List<JsonEntityBasic>) Collection ToDependent JsonEntityBasic"),
            message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 395, 406, Justification = Upstream.GaveNoReason)]
    public override async Task Entity_including_collection_with_json_and_separate_json_projection_AsNoTrackingWithIdentityResolution(
        bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Entity_including_collection_with_json_and_separate_json_projection_AsNoTrackingWithIdentityResolution(async)))
            .Message;

        Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: EntityBasic.JsonEntityBasics (List<JsonEntityBasic>) Collection ToDependent JsonEntityBasic"),
            message);
    }

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 408, 410, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_FirstOrDefault_on_json_scalar(bool async)
        => base.Group_by_FirstOrDefault_on_json_scalar(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 412, 414, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_First_on_json_scalar(bool async)
        => base.Group_by_First_on_json_scalar(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 416, 418, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_json_scalar_Orderby_json_scalar_FirstOrDefault(bool async)
        => base.Group_by_json_scalar_Orderby_json_scalar_FirstOrDefault(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 420, 422, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_json_scalar_Skip_First_project_json_scalar(bool async)
        => base.Group_by_json_scalar_Skip_First_project_json_scalar(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 424, 426, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_on_json_scalar(bool async)
        => base.Group_by_on_json_scalar(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 428, 430, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_on_json_scalar_using_collection_indexer(bool async)
        => base.Group_by_on_json_scalar_using_collection_indexer(async);

    [StoreIssue(IssueTracker.EfCore, 17313, UpstreamRepository.EfCore, Source, 432, 434, Skip = true, Justification = "issue #17313")]
    [Theory(Skip = "issue #17313")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Group_by_Skip_Take_on_json_scalar(bool async)
        => base.Group_by_Skip_Take_on_json_scalar(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 436, 449, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_all_types_entity_projection(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_all_types_entity_projection(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 451, 464, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_all_types_projection_from_owned_entity_reference(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_all_types_projection_from_owned_entity_reference(a);


            });
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 466, 483, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Json_all_types_projection_individual_properties(bool async)
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_all_types_projection_individual_properties(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 485, 498, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_boolean_predicate(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_boolean_predicate(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 500, 512, Justification = Upstream.GaveNoReason)]
    public override Task Json_boolean_predicate_negated(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_boolean_predicate_negated(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 514, 526, Justification = Upstream.GaveNoReason)]
    public override Task Json_boolean_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_boolean_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 528, 540, Justification = Upstream.GaveNoReason)]
    public override Task Json_boolean_projection_negated(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_boolean_projection_negated(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 542, 543, Justification = Upstream.GaveNoReason)]
    public override Task Json_branch_collection_distinct_and_other_collection(bool async)
        => AssertTranslationFailed(() => base.Json_branch_collection_distinct_and_other_collection(async));

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 545, 553, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_after_collection_index_in_projection_using_constant_when_owner_is_not_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_after_collection_index_in_projection_using_constant_when_owner_is_not_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 555, 563, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_after_collection_index_in_projection_using_constant_when_owner_is_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_after_collection_index_in_projection_using_constant_when_owner_is_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 565, 573, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_after_collection_index_in_projection_using_parameter_when_owner_is_not_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_after_collection_index_in_projection_using_parameter_when_owner_is_not_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 575, 583, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_after_collection_index_in_projection_using_parameter_when_owner_is_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_after_collection_index_in_projection_using_parameter_when_owner_is_present(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 585, 592, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_anonymous_projection_distinct_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_anonymous_projection_distinct_in_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 594, 609, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Any_with_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_Any_with_predicate(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 611, 612, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Distinct_Count_with_predicate(bool async)
        => AssertTranslationFailed(() => base.Json_collection_Distinct_Count_with_predicate(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 614, 615, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_distinct_in_projection(bool async)
        => AssertTranslationFailed(() => base.Json_collection_distinct_in_projection(async));

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 617, 625, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_ElementAtOrDefault_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAtOrDefault_in_projection(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 627, 635, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_ElementAtOrDefault_project_collection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAtOrDefault_project_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 637, 653, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Json_collection_ElementAt_and_pushdown(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAt_and_pushdown(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 655, 667, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_ElementAt_in_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAt_in_predicate(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 669, 677, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_ElementAt_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAt_in_projection(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 679, 687, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_ElementAt_project_collection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_ElementAt_project_collection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 689, 705, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_filter_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_filter_in_projection(async);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 707, 714, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_predicate_nested_mix(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base.Json_collection_index_in_predicate_nested_mix(async)))
            .Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 716, 723, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 716, 723, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the key and entity through positional carrier members.",
        DeviationNote = "Item1/Item2 replace Id/x; the indexed predicate and expected Cosmos refusal are unchanged.")]
    public override async Task Json_collection_index_in_predicate_using_column(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception = await AssertCosmosExceptionAsync(() => base.Json_collection_index_in_predicate_using_column(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 725, 733, Case = "async: False", Skip = true, Justification = "Always throws for sync.")]
    [StoreLimit(UpstreamRepository.EfCore, Source, 725, 733, Case = "async: True", Justification = Upstream.GaveNoReason, Deviation = DeviationKind.StoreExceptionAsData)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the key and entity through positional carrier members.",
        DeviationNote = "Item1/Item2 replace Id/x; the conditional indexed predicate and expected Cosmos refusal are unchanged.")]
    public override async Task Json_collection_index_in_predicate_using_complex_expression1(bool async)
    {
        // Always throws for sync.
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            var exception =
                await AssertCosmosExceptionAsync(() => base.Json_collection_index_in_predicate_using_complex_expression1(async));
        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 735, 736, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_index_in_predicate_using_complex_expression2(bool async)
        => AssertTranslationFailed(() => base.Json_collection_index_in_predicate_using_complex_expression2(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 738, 750, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_index_in_predicate_using_constant(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_predicate_using_constant(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 752, 766, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_index_in_predicate_using_variable(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_predicate_using_variable(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 768, 776, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_basic(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_basic(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 778, 785, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_nested(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base.Json_collection_index_in_projection_nested(async)))
            .Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 787, 794, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_nested_project_collection(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_nested_project_collection(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 796, 803, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_nested_project_collection_anonymous_projection(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_nested_project_collection_anonymous_projection(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 805, 812, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_nested_project_reference(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_nested_project_reference(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 814, 821, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_nested_project_scalar(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_nested_project_scalar(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 823, 831, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_project_collection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_project_collection(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 833, 841, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_column(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_column(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 843, 851, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_constant_when_owner_is_not_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_constant_when_owner_is_not_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 853, 861, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_constant_when_owner_is_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_constant_when_owner_is_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 863, 871, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_parameter(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_parameter(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 873, 881, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_parameter_when_owner_is_not_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_parameter_when_owner_is_not_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 883, 891, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_parameter_when_owner_is_present(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_using_parameter_when_owner_is_present(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 893, 895, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method(bool async)
        => base.Json_collection_index_in_projection_using_untranslatable_client_method(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 897, 900, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_index_in_projection_using_untranslatable_client_method2(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_index_in_projection_using_untranslatable_client_method2(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 902, 909, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_when_owner_is_not_present_misc1(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_when_owner_is_not_present_misc1(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 911, 919, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_when_owner_is_not_present_misc2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_when_owner_is_not_present_misc2(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 921, 928, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_when_owner_is_not_present_multiple(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_when_owner_is_not_present_multiple(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 930, 937, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_when_owner_is_present_misc1(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_when_owner_is_present_misc1(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 939, 947, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_in_projection_when_owner_is_present_misc2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_in_projection_when_owner_is_present_misc2(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 949, 956, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_in_projection_when_owner_is_present_multiple(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_in_projection_when_owner_is_present_multiple(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 958, 966, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_outside_bounds(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_outside_bounds(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 968, 976, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_index_outside_bounds2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_index_outside_bounds2(a);


            });

    [StoreDefect("1.24", UpstreamRepository.EfCore, Source, 978, 980, Skip = true, Justification = "returns \"wrong\" results by design - see #34351 for more context")]
    // returns "wrong" results by design - see #34351 for more context
    public override Task Json_collection_index_outside_bounds_with_property_access(bool async)
        => throw SkipException.ForSkip("returns \"wrong\" results by design - see #34351 for more context");

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 982, 989, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_with_expression_Select_ElementAt(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_with_expression_Select_ElementAt(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 991, 998, Justification = Upstream.GaveNoReason)]
    public override async Task Json_collection_index_with_parameter_Select_ElementAt(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_collection_index_with_parameter_Select_ElementAt(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34004, UpstreamRepository.EfCore, Source, 1000, 1002, Skip = true, Justification = "issue #34004")]
    [Theory(Skip = "issue #34004")]
    [MemberData(nameof(IsAsyncData))] // anonymous projection
    public override Task Json_collection_in_projection_with_anonymous_projection_of_scalars(bool async)
        => base.Json_collection_in_projection_with_anonymous_projection_of_scalars(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1004, 1017, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_in_projection_with_composition_count(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_in_projection_with_composition_count(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34004, UpstreamRepository.EfCore, Source, 1019, 1021, Skip = true, Justification = "issue #34004")]
    [Theory(Skip = "issue #34004")]
    [MemberData(nameof(IsAsyncData))] // anonymous projection
    public override Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(bool async)
        => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_primitive_arrays(async);

    [StoreIssue(IssueTracker.EfCore, 34004, UpstreamRepository.EfCore, Source, 1023, 1025, Skip = true, Justification = "issue #34004")]
    [Theory(Skip = "issue #34004")]
    [MemberData(nameof(IsAsyncData))] // anonymous projection
    public override Task Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(bool async)
        => base.Json_collection_in_projection_with_composition_where_and_anonymous_projection_of_scalars(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1027, 1043, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_leaf_filter_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_leaf_filter_in_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1045, 1057, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_of_primitives_contains_in_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_of_primitives_contains_in_predicate(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1059, 1072, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_of_primitives_index_used_in_orderby(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_of_primitives_index_used_in_orderby(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1074, 1086, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_of_primitives_index_used_in_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_of_primitives_index_used_in_predicate(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34026, UpstreamRepository.EfCore, Source, 1088, 1102, Skip = true, Justification = "issue #34026")]
    [Theory(Skip = "issue #34026")]
    [MemberData(nameof(IsAsyncData))] //enums property is ignored
    public override Task Json_collection_of_primitives_index_used_in_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_of_primitives_index_used_in_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1104, 1117, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_of_primitives_SelectMany(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_of_primitives_SelectMany(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1119, 1122, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_OrderByDescending_Skip_ElementAt(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_OrderByDescending_Skip_ElementAt(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries + Environment.NewLine + CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreIssue(IssueTracker.EfCore, 34335, UpstreamRepository.EfCore, Source, 1124, 1126, Skip = true, Justification = "issue #34335")]
    [Theory(Skip = "issue #34335")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_collection_Select_entity_collection_ElementAt(bool async)
        => base.Json_collection_Select_entity_collection_ElementAt(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1128, 1131, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Select_entity_ElementAt(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_Select_entity_ElementAt(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1133, 1136, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Select_entity_in_anonymous_object_ElementAt(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_Select_entity_in_anonymous_object_ElementAt(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1138, 1141, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Select_entity_with_initializer_ElementAt(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_Select_entity_with_initializer_ElementAt(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1143, 1157, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Skip(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_Skip(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1159, 1162, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_skip_take_in_projection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_skip_take_in_projection(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1164, 1167, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_skip_take_in_projection_project_into_anonymous_type(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_skip_take_in_projection_project_into_anonymous_type(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1169, 1172, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_collection_skip_take_in_projection_with_json_reference_access_as_final_operation(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1174, 1190, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_Where_ElementAt(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                // TODO: note the enum value -3
                await base.Json_collection_Where_ElementAt(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1192, 1207, Justification = Upstream.GaveNoReason)]
    public override Task Json_collection_within_collection_Count(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_collection_within_collection_Count(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1209, 1216, Justification = Upstream.GaveNoReason)]
    public override Task Json_entity_backtracking(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_entity_backtracking(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1218, 1230, Justification = Upstream.GaveNoReason)]
    public override Task Json_entity_with_inheritance_basic_projection(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_entity_with_inheritance_basic_projection(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1232, 1244, Justification = Upstream.GaveNoReason)]
    public override Task Json_entity_with_inheritance_project_derived(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_entity_with_inheritance_project_derived(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 1246, 1254, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_entity_with_inheritance_project_navigations(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_entity_with_inheritance_project_navigations(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 1256, 1264, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_entity_with_inheritance_project_navigations_on_derived(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_entity_with_inheritance_project_navigations_on_derived(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1266, 1267, Justification = Upstream.GaveNoReason)]
    public override Task Json_leaf_collection_distinct_and_other_collection(bool async)
        => AssertTranslationFailed(() => base.Json_leaf_collection_distinct_and_other_collection(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1269, 1270, Justification = Upstream.GaveNoReason)]
    public override Task Json_multiple_collection_projections(bool async)
        => AssertTranslationFailed(() => base.Json_multiple_collection_projections(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1272, 1273, Justification = Upstream.GaveNoReason)]
    public override Task Json_nested_collection_anonymous_projection_in_projection(bool async)
        => AssertTranslationFailed(() => base.Json_nested_collection_anonymous_projection_in_projection(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1275, 1278, Justification = Upstream.GaveNoReason)]
    public override Task Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution(
        bool async)
        => AssertTranslationFailed(()
            => base.Json_nested_collection_anonymous_projection_of_primitives_in_projection_NoTrackingWithIdentityResolution(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1280, 1281, Justification = Upstream.GaveNoReason)]
    public override Task Json_nested_collection_filter_in_projection(bool async)
        => AssertTranslationFailed(() => base.Json_nested_collection_filter_in_projection(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1283, 1295, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_int_zero_one(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_int_zero_one(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1297, 1309, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_int_zero_one_with_explicit_comparison(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_int_zero_one_with_explicit_comparison(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1311, 1323, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_string_True_False(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_string_True_False(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1325, 1337, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_string_True_False_with_explicit_comparison(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_string_True_False_with_explicit_comparison(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1339, 1351, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_string_Y_N(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_string_Y_N(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1353, 1365, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_bool_converted_to_string_Y_N_with_explicit_comparison(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_bool_converted_to_string_Y_N_with_explicit_comparison(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1367, 1380, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_byte(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_byte(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1382, 1395, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_byte_array(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_byte_array(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1397, 1410, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_character(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_character(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1412, 1425, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_dateonly(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_dateonly(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1427, 1440, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_datetime(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_datetime(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1442, 1455, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_datetimeoffset(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_datetimeoffset(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1457, 1470, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_decimal(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_decimal(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1472, 1485, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_default_string(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_default_string(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1487, 1500, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_double(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_double(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1502, 1515, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_enum(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_enum(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1517, 1530, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_enumwithintconverter(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_enumwithintconverter(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1532, 1545, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_guid(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_guid(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1547, 1560, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_int16(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_int16(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1562, 1575, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_int32(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_int32(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1577, 1590, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_int64(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_int64(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1592, 1604, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_int_zero_one_converted_to_bool(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_int_zero_one_converted_to_bool(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1606, 1619, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_max_length_string(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_max_length_string(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1621, 1634, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableenum1(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenum1(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1636, 1649, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableenum2(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenum2(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1651, 1664, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableenumwithconverter1(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenumwithconverter1(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1666, 1679, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableenumwithconverter2(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenumwithconverter2(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1681, 1694, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableenumwithconverterthathandlesnulls1(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenumwithconverterthathandlesnulls1(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1696, 1703, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_nullableenumwithconverterthathandlesnulls2(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableenumwithconverterthathandlesnulls2(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1705, 1718, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableint321(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableint321(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1720, 1733, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_nullableint322(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_nullableint322(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1735, 1748, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_signedbyte(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_signedbyte(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1750, 1763, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_single(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_single(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1765, 1778, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_string_condition(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_string_condition(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1780, 1792, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_string_True_False_converted_to_bool(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_string_True_False_converted_to_bool(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1794, 1806, Justification = Upstream.GaveNoReason)]
    public override Task Json_predicate_on_string_Y_N_converted_to_bool(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_string_Y_N_converted_to_bool(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1808, 1821, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_timeonly(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_timeonly(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1823, 1836, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_timespan(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_timespan(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1838, 1851, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_unisgnedint16(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_unisgnedint16(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1853, 1866, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_unsignedint32(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_unsignedint32(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1868, 1881, Skip = true, Justification = "Test does not run on CI")]
    public override Task Json_predicate_on_unsignedint64(bool async)
    {
        SkipOnCI();
        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_predicate_on_unsignedint64(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1883, 1884, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_collection_element_and_reference_AsNoTrackingWithIdentityResolution(bool async)
        => AssertTranslationFailed(() => base.Json_projection_collection_element_and_reference_AsNoTrackingWithIdentityResolution(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1886, 1887, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_deduplication_with_collection_indexer_in_original(bool async)
        => AssertTranslationFailed(() => base.Json_projection_deduplication_with_collection_indexer_in_original(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 1889, 1890, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_deduplication_with_collection_indexer_in_target(bool async)
        => AssertTranslationFailed(() => base.Json_projection_deduplication_with_collection_indexer_in_target(async));

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 1892, 1899, Justification = Upstream.GaveNoReason)]
    public override async Task Json_projection_deduplication_with_collection_in_original_and_collection_indexer_in_target(bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(()
            => base.Json_projection_deduplication_with_collection_in_original_and_collection_indexer_in_target(async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1901, 1913, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_enum_with_custom_conversion(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_projection_enum_with_custom_conversion(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 1915, 1918, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_nested_collection_and_element_correct_order_AsNoTrackingWithIdentityResolution(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_projection_nested_collection_and_element_correct_order_AsNoTrackingWithIdentityResolution(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 1920, 1930, Justification = Upstream.GaveNoReason)]
    public override async Task
        Json_projection_nested_collection_element_using_parameter_and_the_owner_in_correct_order_AsNoTrackingWithIdentityResolution(
            bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base
            .Json_projection_nested_collection_element_using_parameter_and_the_owner_in_correct_order_AsNoTrackingWithIdentityResolution(
                async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1932, 1944, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Json_projection_nothing_interesting_AsNoTrackingWithIdentityResolution(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_projection_nothing_interesting_AsNoTrackingWithIdentityResolution(a);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 1946, 1956, Justification = Upstream.GaveNoReason)]
    public override async Task
        Json_projection_only_second_element_through_collection_element_constant_projected_nested_AsNoTrackingWithIdentityResolution(
            bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base
            .Json_projection_only_second_element_through_collection_element_constant_projected_nested_AsNoTrackingWithIdentityResolution(
                async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 1958, 1968, Justification = Upstream.GaveNoReason)]
    public override async Task
        Json_projection_only_second_element_through_collection_element_parameter_projected_nested_AsNoTrackingWithIdentityResolution(
            bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base
            .Json_projection_only_second_element_through_collection_element_parameter_projected_nested_AsNoTrackingWithIdentityResolution(
                async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 1970, 1991, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]
    public override Task Json_projection_owner_entity_AsNoTrackingWithIdentityResolution(bool async)
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        return Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_projection_owner_entity_AsNoTrackingWithIdentityResolution(a);


            });
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1993, 1996, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_reference_collection_and_collection_element_nested_AsNoTrackingWithIdentityResolution(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_projection_reference_collection_and_collection_element_nested_AsNoTrackingWithIdentityResolution(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 1998, 2001, Justification = Upstream.GaveNoReason)]
    public override Task Json_projection_second_element_projected_before_owner_as_well_as_root_AsNoTrackingWithIdentityResolution(
        bool async)
        => AssertTranslationFailed(()
            => base.Json_projection_second_element_projected_before_owner_as_well_as_root_AsNoTrackingWithIdentityResolution(async));

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 2003, 2013, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Json_projection_second_element_projected_before_owner_nested_as_well_as_root_AsNoTrackingWithIdentityResolution(
        bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_projection_second_element_projected_before_owner_nested_as_well_as_root_AsNoTrackingWithIdentityResolution(
                    a);


            });

    [StoreIssue(IssueTracker.EfCore, 34348, UpstreamRepository.EfCore, Source, 2015, 2025, Justification = Upstream.GaveNoReason)]
    public override async Task
        Json_projection_second_element_through_collection_element_constant_different_values_projected_before_owner_nested_AsNoTrackingWithIdentityResolution(
            bool async)
    {
        var message = (await Assert.ThrowsAsync<NotImplementedException>(() => base
            .Json_projection_second_element_through_collection_element_constant_different_values_projected_before_owner_nested_AsNoTrackingWithIdentityResolution(
                async))).Message;

        // issue #34348
        Assert.Equal(NotImplementedBindPropertyMessage, message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2027, 2034, Justification = Upstream.GaveNoReason)]
    public override Task
        Json_projection_second_element_through_collection_element_constant_projected_after_owner_nested_AsNoTrackingWithIdentityResolution(
            bool async)
        => AssertTranslationFailedWithDetails(
            () => base
                .Json_projection_second_element_through_collection_element_constant_projected_after_owner_nested_AsNoTrackingWithIdentityResolution(
                    async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2036, 2043, Justification = Upstream.GaveNoReason)]
    public override Task
        Json_projection_second_element_through_collection_element_parameter_correctly_projected_after_owner_nested_AsNoTrackingWithIdentityResolution(
            bool async)
        => AssertTranslationFailedWithDetails(
            () => base
                .Json_projection_second_element_through_collection_element_parameter_correctly_projected_after_owner_nested_AsNoTrackingWithIdentityResolution(
                    async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2045, 2057, Justification = Upstream.GaveNoReason)]
    public override Task Json_property_in_predicate(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_property_in_predicate(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2059, 2071, Justification = Upstream.GaveNoReason)]
    public override Task Json_scalar_length(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_scalar_length(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2073, 2085, Justification = Upstream.GaveNoReason)]
    public override Task Json_scalar_optional_null_semantics(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_scalar_optional_null_semantics(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2087, 2099, Justification = Upstream.GaveNoReason)]
    public override Task Json_scalar_required_null_semantics(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_scalar_required_null_semantics(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2101, 2104, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_property_pushdown_length(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_subquery_property_pushdown_length(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2106, 2109, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_property(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_subquery_reference_pushdown_property(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2111, 2114, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_subquery_reference_pushdown_reference(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2116, 2117, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_reference_anonymous_projection(bool async)
        => base.Json_subquery_reference_pushdown_reference_anonymous_projection(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2119, 2120, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_reference_pushdown_anonymous_projection(bool async)
        => base.Json_subquery_reference_pushdown_reference_pushdown_anonymous_projection(async);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2122, 2125, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_reference_pushdown_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_subquery_reference_pushdown_reference_pushdown_collection(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2127, 2130, Justification = Upstream.GaveNoReason)]
    public override Task Json_subquery_reference_pushdown_reference_pushdown_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_subquery_reference_pushdown_reference_pushdown_reference(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2132, 2141, Justification = Upstream.GaveNoReason)]
    public override async Task Json_with_include_on_entity_collection(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Json_with_include_on_entity_collection(async)))
            .Message;

        Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: JsonEntityBasic.EntityCollection (List<JsonEntityBasicForCollection>) Collection ToDependent JsonEntityBasicForCollection Inverse: Parent"),
            message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2143, 2146, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_include_on_entity_collection_and_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_with_include_on_entity_collection_and_reference(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasicForReference), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2148, 2151, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_include_on_entity_reference(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_with_include_on_entity_reference(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasicForReference), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2153, 2165, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_include_on_json_entity(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Json_with_include_on_json_entity(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2167, 2172, Justification = Upstream.GaveNoReason)]
    public override async Task Json_with_projection_of_json_collection_and_entity_collection(bool async)
        => Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: JsonEntityBasic.EntityCollection (List<JsonEntityBasicForCollection>) Collection ToDependent JsonEntityBasicForCollection Inverse: Parent"),
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Json_with_projection_of_json_collection_and_entity_collection(async))).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2174, 2177, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_projection_of_json_collection_element_and_entity_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_with_projection_of_json_collection_element_and_entity_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasicForReference), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2179, 2184, Justification = Upstream.GaveNoReason)]
    public override async Task Json_with_projection_of_json_collection_leaf_and_entity_collection(bool async)
        => Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: JsonEntityBasic.EntityCollection (List<JsonEntityBasicForCollection>) Collection ToDependent JsonEntityBasicForCollection Inverse: Parent"),
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Json_with_projection_of_json_collection_leaf_and_entity_collection(async))).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2186, 2191, Justification = Upstream.GaveNoReason)]
    public override async Task Json_with_projection_of_json_reference_and_entity_collection(bool async)
        => Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: JsonEntityBasic.EntityCollection (List<JsonEntityBasicForCollection>) Collection ToDependent JsonEntityBasicForCollection Inverse: Parent"),
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Json_with_projection_of_json_reference_and_entity_collection(async))).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2193, 2198, Justification = Upstream.GaveNoReason)]
    public override async Task Json_with_projection_of_json_reference_leaf_and_entity_collection(bool async)
        => Assert.Equal(
            CosmosStrings.NonEmbeddedIncludeNotSupported(
                "Navigation: JsonEntityBasic.EntityCollection (List<JsonEntityBasicForCollection>) Collection ToDependent JsonEntityBasicForCollection Inverse: Parent"),
            (await Assert.ThrowsAsync<InvalidOperationException>(()
                => base.Json_with_projection_of_json_reference_leaf_and_entity_collection(async))).Message);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2200, 2203, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_projection_of_mix_of_json_collections_json_references_and_entity_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_with_projection_of_mix_of_json_collections_json_references_and_entity_collection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasicForReference), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2205, 2208, Justification = Upstream.GaveNoReason)]
    public override Task Json_with_projection_of_multiple_json_references_and_entity_collection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Json_with_projection_of_multiple_json_references_and_entity_collection(async),
            CosmosStrings.LimitOffsetNotSupportedInSubqueries);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2210, 2213, Justification = Upstream.GaveNoReason)]
    public override Task LeftJoin_json_entities(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.LeftJoin_json_entities(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasic), nameof(JsonEntitySingleOwned)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2215, 2218, Justification = Upstream.GaveNoReason)]
    public override Task RightJoin_json_entities(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.RightJoin_json_entities(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntitySingleOwned), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2220, 2223, Justification = Upstream.GaveNoReason)]
    public override Task Left_join_json_entities_complex_projection(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Left_join_json_entities_complex_projection(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntityBasic), nameof(JsonEntitySingleOwned)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2225, 2228, Justification = Upstream.GaveNoReason)]
    public override Task Left_join_json_entities_complex_projection_json_being_inner(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Left_join_json_entities_complex_projection_json_being_inner(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntitySingleOwned), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2230, 2233, Justification = Upstream.GaveNoReason)]
    public override Task Left_join_json_entities_json_being_inner(bool async)
        => AssertTranslationFailedWithDetails(
            () => base.Left_join_json_entities_json_being_inner(async),
            CosmosStrings.MultipleRootEntityTypesReferencedInQuery(nameof(JsonEntitySingleOwned), nameof(JsonEntityBasic)));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2235, 2247, Justification = Upstream.GaveNoReason)]
    public override Task Project_entity_with_single_owned(bool async)
        => Fixture.NoSyncTest(
            async, async a =>
            {
                await base.Project_entity_with_single_owned(a);


            });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2249, 2250, Justification = Upstream.GaveNoReason)]
    public override Task Project_json_entity_FirstOrDefault_subquery_with_binding_on_top(bool async)
        => AssertTranslationFailed(() => base.Project_json_entity_FirstOrDefault_subquery_with_binding_on_top(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2252, 2253, Justification = Upstream.GaveNoReason)]
    public override Task Project_json_entity_FirstOrDefault_subquery_with_entity_comparison_on_top(bool async)
        => AssertTranslationFailed(() => base.Project_json_entity_FirstOrDefault_subquery_with_entity_comparison_on_top(async));

    [StoreLimit(UpstreamRepository.EfCore, Source, 2255, 2261, Justification = Upstream.GaveNoReason)]
    public override async Task Project_json_reference_in_tracking_query_fails(bool async)
    {
        var message =
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Project_json_reference_in_tracking_query_fails(async))).Message;

        Assert.Equal(CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner, message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2263, 2270, Justification = Upstream.GaveNoReason)]
    public override async Task Project_json_collection_in_tracking_query_fails(bool async)
    {
        var message =
            (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Project_json_collection_in_tracking_query_fails(async)))
            .Message;

        Assert.Equal(CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner, message);
    }

    [StoreIssue(IssueTracker.EfCore, 34350, UpstreamRepository.EfCore, Source, 2272, 2279, Skip = true, Justification = "issue #34350")]
    [Theory(Skip = "issue #34350")]
    [MemberData(nameof(IsAsyncData))]
    public override async Task Project_json_entity_in_tracking_query_fails_even_when_owner_is_present(bool async)
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(()
            => base.Project_json_entity_in_tracking_query_fails_even_when_owner_is_present(async))).Message;

        Assert.Equal(CoreStrings.OwnedEntitiesCannotBeTrackedWithoutTheirOwner, message);
    }

    protected override void ConfigureWarnings(WarningsConfigurationBuilder builder)
    {
        base.ConfigureWarnings(builder);

        builder.Ignore(CosmosEventId.NoPartitionKeyDefined, CosmosEventId.PrimaryKeyValueNotSet);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2288, 2303, Justification = Upstream.GaveNoReason)]

    public override Task Project_root_with_missing_scalars(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_root_with_missing_scalars(async);


    });

    [StoreIssue(IssueTracker.EfCore, 35702, UpstreamRepository.EfCore, Source, 2305, 2312, Skip = true, Justification = "issue #35702")]
    [Theory(Skip = "issue #35702")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Project_top_level_json_entity_with_missing_scalars(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_top_level_json_entity_with_missing_scalars(async);


    });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2314, 2323, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Project_nested_json_entity_with_missing_scalars(bool async)
    {
        // Throws sync not supported exception for sync
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            await AssertTranslationFailed(() => base.Project_nested_json_entity_with_missing_scalars(async));


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2325, 2336, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Project_top_level_entity_with_null_value_required_scalars(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_top_level_entity_with_null_value_required_scalars(async);


    });

    [Fact]
    public virtual async Task Project_entity_with_null_value_required_scalars()
    {
        var contextFactory = await InitializeNonSharedTest<Context21006>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            onModelCreating: OnModelCreating21006,
            seed: Seed21006);

        await using var context = contextFactory.CreateDbContext();

        var result = await context.Set<Context21006.Entity>().Where(x => x.Id == 4).AsNoTracking().ToListAsync(cancellationToken: TestContext.Current.CancellationToken);

        var nullScalars = result.Single();

        Assert.Equal(default, nullScalars.RequiredReference.Number);
    }

    [Fact]
    public virtual async Task Project_null_value_required_scalar()
    {
        var contextFactory = await InitializeNonSharedTest<Context21006>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            onModelCreating: OnModelCreating21006,
            seed: Seed21006);

        await using var context = contextFactory.CreateDbContext();

        // Same as in 10.0
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Set<Context21006.Entity>().Where(x => x.Id == 4).Select(x => x.RequiredReference.Number).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(
            "Cannot read the Value property of a Nullable object that has no value. Check HasValue before reading Value.",
            ex.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2372, 2381, Justification = Upstream.GaveNoReason)]
    public override Task Project_root_entity_with_missing_required_navigation(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_root_entity_with_missing_required_navigation(async);


    });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2383, 2399, Skip = true, Justification = Upstream.GaveNoReason)]
    public override async Task Project_missing_required_navigation(bool async)
    {
        // Throws sync not supported exception for sync
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            // Cosmos will filter out undefined result
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Project_missing_required_navigation(async));
            Assert.Equal("Sequence contains no elements", ex.Message);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2401, 2410, Justification = Upstream.GaveNoReason)]
    public override Task Project_root_entity_with_null_required_navigation(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_root_entity_with_null_required_navigation(async);


    });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2412, 2423, Justification = Upstream.GaveNoReason)]
    public override Task Project_null_required_navigation(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_null_required_navigation(async);


    });

    [StoreLimit(UpstreamRepository.EfCore, Source, 2425, 2442, Skip = true, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override async Task Project_missing_required_scalar(bool async)
    {
        // Throws sync not supported exception for sync
        if (!async) throw SkipException.ForSkip("Upstream does not execute this synchronous arm.");
        if (async)
        {
            // https://github.com/dotnet/efcore/issues/38298#issuecomment-4726236589
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Project_missing_required_scalar(async));

            Assert.Equal(CosmosStrings.ProjectionUndefined, exception.Message);


        }
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2444, 2455, Justification = Upstream.GaveNoReason)]
    [InfoCarrierDesign(10, Deviation = DeviationKind.SqlDiffers,
        Justification = "Projection reassembly transports the same fields through a positional carrier.",
        DeviationNote = "JSON projection envelope or member names differ; field expressions, predicates, row ordering, and limits match the direct provider.")]
    public override Task Project_null_required_scalar(bool async)
    => Fixture.NoSyncTest(async, async async =>
    {
        await base.Project_null_required_scalar(async);


    });

    protected override void OnModelCreating21006(ModelBuilder modelBuilder)
    {
        base.OnModelCreating21006(modelBuilder);

        modelBuilder.Entity<Context21006.Entity>().ToContainer("Entities");
    }

    protected override async Task Seed21006(Context21006 context)
    {
        await base.Seed21006(context);

        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var missingTopLevel =
            """
{
    "Id": 2,
    "$type": "Entity",
    "Name": "e2",
    "id": "2",
    "Collection":
    [
        {
            "Text": "e2 c1",
            "NestedCollection": [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 c1 c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 c1 c2"
            }
            ],
            "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 c1 nor"
            },
            "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 c1 nrr"
            }
        },
        {
            "Text": "e2 c2",
            "NestedCollection": [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 c2 c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 c2 c2"
            }
            ],
            "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 c2 nor"
            },
            "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 c2 nrr"
            }
        }
    ],
    "OptionalReference": {
        "Text": "e2 or",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 or c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 or c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 or nor"
        },
        "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 or nrr"
        }
    },
    "RequiredReference": {
        "Text": "e2 rr",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 rr c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e2 rr c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 rr nor"
        },
        "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e2 rr nrr"
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingTopLevel,
            CancellationToken.None);

        var missingNested =
            """
{
    "Id": 3,
    "$type": "Entity",
    "Name": "e3",
    "id": "3",
    "Collection":
    [
        {
            "Number": 7.0,
            "Text": "e3 c1",
            "NestedCollection":
            [
                {
                  "Text": "e3 c1 c1"
                },
                {
                  "Text": "e3 c1 c2"
                }
            ],
            "NestedOptionalReference": {
            "Text": "e3 c1 nor"
            },
            "NestedRequiredReference": {
            "Text": "e3 c1 nrr"
            }
        },
        {
            "Number": 7.0,
            "Text": "e3 c2",
            "NestedCollection":
            [
                {
                  "Text": "e3 c2 c1"
                },
                {
                  "Text": "e3 c2 c2"
                }
            ],
            "NestedOptionalReference": {
                "Text": "e3 c2 nor"
            },
            "NestedRequiredReference": {
                "Text": "e3 c2 nrr"
            }
        }
    ],
    "OptionalReference": {
        "Number": 7.0,
        "Text": "e3 or",
        "NestedCollection":
        [
            {
                "Text": "e3 or c1"
            },
            {
                "Text": "e3 or c2"
            }
        ],
        "NestedOptionalReference": {
            "Text": "e3 or nor"
        },
        "NestedRequiredReference": {
            "Text": "e3 or nrr"
        }
    },
    "RequiredReference": {
        "Number": 7.0,
        "Text": "e3 rr",
        "NestedCollection":
        [
            {
                "Text": "e3 rr c1"
            },
            {
                "Text": "e3 rr c2"
            }
        ],
        "NestedOptionalReference": {
            "Text": "e3 rr nor"
        },
        "NestedRequiredReference": {
            "Text": "e3 rr nrr"
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingNested,
            CancellationToken.None);

        var nullTopLevel =
            """
{
    "Id": 4,
    "$type": "Entity",
    "Name": "e4",
    "id": "4",
    "Collection":
    [
        {
            "Number": null,
            "Text": "e4 c1",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e4 c1 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e4 c1 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 c1 nor"
            },
            "NestedRequiredReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 c1 nrr"
            }
        },
        {
            "Number": null,
            "Text": "e4 c2",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e4 c2 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e4 c2 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 c2 nor"
            },
            "NestedRequiredReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 c2 nrr"
            }
        }
    ],
    "OptionalReference": {
        "Number": null,
        "Text": "e4 or",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 or c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 or c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e4 or nor"
        },
        "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e4 or nrr"
        }
    },
    "RequiredReference": {
        "Number": null,
        "Text": "e4 rr",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 rr c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e4 rr c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e4 rr nor"
        },
        "NestedRequiredReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e4 rr nrr"
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            nullTopLevel,
            CancellationToken.None);

        var missingRequiredNav =
            """
{
    "Id": 5,
    "$type": "Entity",
    "Name": "e5",
    "id": "5",
    "Collection":
    [
        {
            "Number": 7.0,
            "Text": "e5 c1",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e5 c1 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e5 c1 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 c1 nor"
            }
        },
        {
            "Number": 7.0,
            "Text": "e5 c2",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e5 c2 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e5 c2 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 c2 nor"
            }
        }
    ],
    "OptionalReference": {
        "Number": 7.0,
        "Text": "e5 or",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 or c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 or c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e5 or nor"
        }
    },
    "RequiredReference": {
        "Number": 7.0,
        "Text": "e5 rr",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 rr c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e5 rr c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e5 rr nor"
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingRequiredNav,
            CancellationToken.None);

        var nullRequiredNav =
            """
{
    "Id": 6,
    "$type": "Entity",
    "Name": "e6",
    "id": "6",
    "Collection":
    [
        {
            "Number": 7.0,
            "Text": "e6 c1",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e6 c1 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e6 c1 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 c1 nor"
            },
            "NestedRequiredReference": null
        },
        {
            "Number": 7.0,
            "Text": "e6 c2",
            "NestedCollection":
            [
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e6 c2 c1"
                },
                {
                    "DoB": "2000-01-01T00:00:00",
                    "Text": "e6 c2 c2"
                }
            ],
            "NestedOptionalReference": {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 c2 nor"
            },
            "NestedRequiredReference": null
        }
    ],
    "OptionalReference": {
        "Number": 7.0,
        "Text": "e6 or",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 or c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 or c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e6 or nor"
        },
        "NestedRequiredReference": null
    },
    "RequiredReference": {
        "Number": 7.0,
        "Text": "e6 rr",
        "NestedCollection":
        [
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 rr c1"
            },
            {
                "DoB": "2000-01-01T00:00:00",
                "Text": "e6 rr c2"
            }
        ],
        "NestedOptionalReference": {
            "DoB": "2000-01-01T00:00:00",
            "Text": "e6 rr nor"
        },
        "NestedRequiredReference": null
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            nullRequiredNav,
            CancellationToken.None);
    }

    [StoreDefect("1.24", UpstreamRepository.EfCore, Source, 2973, 2980, Skip = true, Justification = "Cosmos returns unexpected number of results (i.e. not returning row with non-existent NullableScalar this is by design behavior in Cosmos, so we just skip the test to avoid validation error")]

    // Cosmos returns unexpected number of results (i.e. not returning row with non-existent NullableScalar
    // this is by design behavior in Cosmos, so we just skip the test to avoid validation error
    public override Task Can_project_nullable_json_property_when_the_element_in_json_is_not_present()
        => throw SkipException.ForSkip("Cosmos returns unexpected number of results (i.e. not returning row with non-existent NullableScalar this is by design behavior in Cosmos, so we just skip the test to avoid validation error");

    protected override void OnModelCreating29219(ModelBuilder modelBuilder)
    {
        base.OnModelCreating29219(modelBuilder);

        modelBuilder.Entity<Context29219.MyEntity>().ToContainer("Entities");
    }

    protected override async Task Seed29219(DbContext context)
    {
        await base.Seed29219(context);

        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var missingNullableScalars =
            """
{
    "Id": 3,
    "$type": "MyEntity",
    "id": "3",
    "Collection":
    [
        {
            "NonNullableScalar" : 10001
        }
    ],
    "Reference": {
        "NonNullableScalar" : 30
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingNullableScalars,
            CancellationToken.None);
    }

    [StoreIssue(IssueTracker.EfCore, 35702, UpstreamRepository.EfCore, Source, 3021, 3027, Skip = true, Justification = "issue #35702")]

    [Theory(Skip = "issue #35702")]
    [MemberData(nameof(IsAsyncData))]
    public override Task Missing_navigation_works_with_deduplication(bool async)
        => base.Missing_navigation_works_with_deduplication(async);

    [StoreDefect("1.24", UpstreamRepository.EfCore, Source, 3029, 3031, Skip = true, Justification = "missing array comes out as empty on Cosmos")]
    // missing array comes out as empty on Cosmos
    public override Task Accessing_missing_navigation_works()
        => throw SkipException.ForSkip("missing array comes out as empty on Cosmos");

    protected override void OnModelCreating30028(ModelBuilder modelBuilder)
    {
        base.OnModelCreating30028(modelBuilder);

        modelBuilder.Entity<Context30028.MyEntity>().ToContainer("Entities");
    }

    protected override async Task Seed30028(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var complete =
            """
{
    "Id": 1,
    "$type": "MyEntity",
    "id": "1",
    "Json": {
        "RootName":"e1",
        "Collection":
        [
            {
                "BranchName":"e1 c1",
                "Nested":{
                    "LeafName":"e1 c1 l"
                }
            },
            {
                "BranchName":"e1 c2",
                "Nested":{
                    "LeafName":"e1 c2 l"
                }
            }
        ],
        "OptionalReference":{
            "BranchName":"e1 or",
            "Nested":{
                "LeafName":"e1 or l"
            }
        },
        "RequiredReference":{
            "BranchName":"e1 rr",
            "Nested":{
                "LeafName":"e1 rr l"
            }
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            complete,
            CancellationToken.None);

        var missingCollection =
            """
{
    "Id": 2,
    "$type": "MyEntity",
    "id": "2",
    "Json": {
        "RootName":"e2",
        "OptionalReference":{
            "BranchName":"e2 or",
            "Nested":{
                "LeafName":"e2 or l"
            }
        },
        "RequiredReference":{
            "BranchName":"e2 rr",
            "Nested":{
                "LeafName":"e2 rr l"
            }
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingCollection,
            CancellationToken.None);

        var missingOptionalReference =
            """
{
    "Id": 3,
    "$type": "MyEntity",
    "id": "3",
    "Json": {
        "RootName":"e3",
        "Collection":
        [
            {
                "BranchName":"e3 c1",
                "Nested":{
                    "LeafName":"e3 c1 l"
                }
            },
            {
                "BranchName":"e3 c2",
                "Nested":{
                    "LeafName":"e3 c2 l"
                }
            }
        ],
        "RequiredReference":{
            "BranchName":"e3 rr",
            "Nested":{
                "LeafName":"e3 rr l"
            }
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingOptionalReference,
            CancellationToken.None);

        var missingRequiredReference =
            """
{
    "Id": 4,
    "$type": "MyEntity",
    "id": "4",
    "Json": {
        "RootName":"e4",
        "Collection":
        [
            {
                "BranchName":"e4 c1",
                "Nested":{
                    "LeafName":"e4 c1 l"
                }
            },
            {
                "BranchName":"e4 c2",
                "Nested":{
                    "LeafName":"e4 c2 l"
                }
            }
        ],
        "OptionalReference":{
            "BranchName":"e4 or",
            "Nested":{
                "LeafName":"e4 or l"
            }
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            missingRequiredReference,
            CancellationToken.None);
    }

    protected override void OnModelCreating33046(ModelBuilder modelBuilder)
    {
        base.OnModelCreating33046(modelBuilder);

        modelBuilder.Entity<Context33046.Review>().ToContainer("Reviews");
    }

    protected override async Task Seed33046(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Reviews");

        var json =
            """
{
    "Id": 1,
    "$type": "Review",
    "id": "1",
    "Rounds":
    [
        {
            "RoundNumber":11,
            "SubRounds":
            [
                {
                    "SubRoundNumber":111
                },
                {
                    "SubRoundNumber":112
                }
            ]
        }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json,
            CancellationToken.None);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3243, 3253, Justification = Upstream.GaveNoReason)]

    public override async Task Try_project_collection_but_JSON_is_entity()
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Try_project_collection_but_JSON_is_entity()))
            .Message;

        Assert.Equal(CoreStrings.JsonReaderInvalidTokenType(JsonTokenType.StartObject), message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 3255, 3261, Justification = Upstream.GaveNoReason)]
    public override async Task Try_project_reference_but_JSON_is_collection()
    {
        var message = (await Assert.ThrowsAsync<InvalidOperationException>(() => base.Try_project_reference_but_JSON_is_collection()))
            .Message;

        Assert.Equal(CoreStrings.JsonReaderInvalidTokenType(JsonTokenType.StartArray), message);
    }

    protected override void OnModelCreating34960(ModelBuilder modelBuilder)
    {
        base.OnModelCreating34960(modelBuilder);
        modelBuilder.Entity<Context34960.Entity>().ToContainer("Entities");
        modelBuilder.Entity<Context34960.JunkEntity>().ToContainer("Junk");
    }

    protected override async Task Seed34960(Context34960 context)
    {
        await base.Seed34960(context);

        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var json =
            """
{
    "Id": 4,
    "$type": "Entity",
    "id": "4",
    "Collection": null,
    "Reference": null
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json,
            CancellationToken.None);

        var junkContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Junk");

        var objectWhereCollectionShouldBe =
            """
{
    "Id": 1,
    "$type": "JunkEntity",
    "id": "1",
    "Collection": { "DoB":"2000-01-01T00:00:00","Text":"junk" },
    "Reference": null
}
""";

        await CreateCustomEntityHelperAsync(
            junkContainer,
            objectWhereCollectionShouldBe,
            CancellationToken.None);

        var collectionWhereEntityShouldBe =
            """
{
    "Id": 2,
    "$type": "JunkEntity",
    "id": "2",
    "Collection": null,
    "Reference": [{ "DoB":"2000-01-01T00:00:00","Text":"junk" }]
}
""";

        await CreateCustomEntityHelperAsync(
            junkContainer,
            collectionWhereEntityShouldBe,
            CancellationToken.None);
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 3329, 3339, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]

    public override async Task Project_element_of_json_array_of_primitives()
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        await base.Project_element_of_json_array_of_primitives();
    }

    protected override void OnModelCreatingArrayOfPrimitives(ModelBuilder modelBuilder)
        => base.OnModelCreatingArrayOfPrimitives(modelBuilder);

    protected override void OnModelCreatingJunkInJson(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingJunkInJson(modelBuilder);

        modelBuilder.Entity<ContextJunkInJson.MyEntity>().ToContainer("Entities");
    }

    protected override async Task SeedJunkInJson(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var json =
            """
{
    "Id": 1,
    "$type": "MyEntity",
    "id": "1",
    "Collection":
    [
        {
            "JunkReference": {
                "Something":"SomeValue"
            },
            "Name":"c11",
            "JunkProperty1":50,
            "Number":11.5,
            "JunkCollection1":[],
            "JunkCollection2":
            [
                {
                    "Foo":"junk value"
                }
            ],
            "NestedCollection":
            [
                {
                    "DoB":"2002-04-01T00:00:00",
                    "DummyProp":"Dummy value"
                },
                {
                    "DoB":"2002-04-02T00:00:00",
                    "DummyReference":{
                        "Foo":5
                    }
                }
            ],
            "NestedReference":{
                "DoB":"2002-03-01T00:00:00"
            }
        },
        {
            "Name":"c12",
            "Number":12.5,
            "NestedCollection":
            [
                {
                    "DoB":"2002-06-01T00:00:00"
                },
                {
                    "DoB":"2002-06-02T00:00:00"
                }
            ],
            "NestedDummy":59,
            "NestedReference":{
                "DoB":"2002-05-01T00:00:00"
            }
        }
    ],
    "CollectionWithCtor":
    [
        {
            "MyBool":true,
            "Name":"c11 ctor",
            "JunkReference":{
                "Something":"SomeValue",
                "JunkCollection":
                [
                    {
                        "Foo":"junk value"
                    }
                ]
            },
            "NestedCollection":
            [
                {
                    "DoB":"2002-08-01T00:00:00"
                },
                {
                    "DoB":"2002-08-02T00:00:00"
                }
            ],
            "NestedReference":{
                "DoB":"2002-07-01T00:00:00"
            }
        },
        {
            "MyBool":false,
            "Name":"c12 ctor",
            "NestedCollection":
            [
                {
                    "DoB":"2002-10-01T00:00:00"
                },
                {
                    "DoB":"2002-10-02T00:00:00"
                }
            ],
            "JunkCollection":
            [
                {
                    "Foo":"junk value"
                }
            ],
            "NestedReference":{
                "DoB":"2002-09-01T00:00:00"
            }
        }
    ],
    "Reference": {
        "Name":"r1",
        "JunkCollection":
        [
            {
                "Foo":"junk value"
            }
        ],
        "JunkReference":{
            "Something":"SomeValue"
        },
        "Number":1.5,
        "NestedCollection":
        [
            {
                "DoB":"2000-02-01T00:00:00",
                "JunkReference":{
                    "Something":"SomeValue"
                }
            },
            {
                "DoB":"2000-02-02T00:00:00"
            }
        ],
        "NestedReference":{
            "DoB":"2000-01-01T00:00:00"
        }
    },
    "ReferenceWithCtor":{
        "MyBool":true,
        "JunkCollection":
        [
            {
                "Foo":"junk value"
            }
        ],
        "Name":"r1 ctor",
        "JunkReference":{
            "Something":"SomeValue"
        },
        "NestedCollection":
        [
            {
                "DoB":"2001-02-01T00:00:00"
            },
            {
                "DoB":"2001-02-02T00:00:00"
            }
        ],
        "NestedReference":{
            "JunkCollection":
            [
                {
                    "Foo":"junk value"
                }
            ],
            "DoB":"2001-01-01T00:00:00"
        }
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json,
            CancellationToken.None);
    }

    protected override void OnModelCreatingTrickyBuffering(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingTrickyBuffering(modelBuilder);

        modelBuilder.Entity<ContextTrickyBuffering.MyEntity>().ToContainer("Entities");
    }

    protected override async Task SeedTrickyBuffering(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var json =
            """
{
    "Id": 1,
    "$type": "MyEntity",
    "id": "1",
    "Reference": {
        "Name": "r1",
        "Number": 7,
        "JunkReference": {
            "Something": "SomeValue"
        },
        "JunkCollection":
        [
            {
                "Foo": "junk value"
            }
        ],
        "NestedReference": {
            "DoB": "2000-01-01T00:00:00"
        },
        "NestedCollection":
        [
            {
                "DoB": "2000-02-01T00:00:00",
                "JunkReference": {
                    "Something": "SomeValue"
                }
            },
            {
                "DoB": "2000-02-02T00:00:00"
            }
        ]
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json,
            CancellationToken.None);
    }

    [StoreDefect("2.3", UpstreamRepository.EfCore, Source, 3596, 3606, Skip = true, Justification = "Not supported on the Linux Cosmos emulator")]

    public override async Task Project_shadow_properties_from_json_entity()
    {
        // https://github.com/Azure/azure-cosmos-db-emulator-docker/issues/335
        SkipOnLinuxEmulator();

        await base.Project_shadow_properties_from_json_entity();
    }

    protected override void OnModelCreatingShadowProperties(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingShadowProperties(modelBuilder);

        modelBuilder.Entity<ContextShadowProperties.MyEntity>(b =>
        {
            b.ToContainer("Entities");

            //b.OwnsOne(x => x.Reference, b =>
            //{
            //    //      b.ToJson().HasColumnType(JsonColumnType);
            //    b.Property<string>("ShadowString");
            //});

            b.OwnsOne(
                x => x.ReferenceWithCtor, b =>
                {
                    //    b.ToJson().HasColumnType(JsonColumnType);
                    b.Property<int>("Shadow_Int").ToJsonProperty("ShadowInt");
                });

            //b.OwnsMany(
            //    x => x.Collection, b =>
            //    {
            //        //  b.ToJson().HasColumnType(JsonColumnType);
            //        b.Property<double>("ShadowDouble");
            //    });

            //b.OwnsMany(
            //    x => x.CollectionWithCtor, b =>
            //    {
            //        //b.ToJson().HasColumnType(JsonColumnType);
            //        b.Property<byte?>("ShadowNullableByte");
            //    });
        });
    }

    protected override async Task SeedShadowProperties(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var json =
            """
{
    "Id": 1,
    "$type": "MyEntity",
    "id": "1",
    "Name": "e1",
    "Collection":
    [
        {
            "Name":"e1_c1","ShadowDouble":5.5
        },
        {
            "ShadowDouble":20.5,"Name":"e1_c2"
        }
    ],
    "CollectionWithCtor":
    [
        {
            "Name":"e1_c1 ctor","ShadowNullableByte":6
        },
        {
            "ShadowNullableByte":null,"Name":"e1_c2 ctor"
        }
    ],
    "Reference": { "Name":"e1_r", "ShadowString":"Foo" },
    "ReferenceWithCtor": { "ShadowInt":143,"Name":"e1_r ctor" }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json,
            CancellationToken.None);
    }

    protected override void OnModelCreatingLazyLoadingProxies(ModelBuilder modelBuilder)
        => base.OnModelCreatingLazyLoadingProxies(modelBuilder);

    protected override void OnModelCreatingNotICollection(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingNotICollection(modelBuilder);

        modelBuilder.Entity<ContextNotICollection.MyEntity>().ToContainer("Entities");
    }

    protected override async Task SeedNotICollection(DbContext context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var json1 =
            """
{
    "Id": 1,
    "$type": "MyEntity",
    "id": "1",
    "Json":
    {
        "Collection":
        [
            {
                "Bar":11,"Foo":"c11"
            },
            {
                "Bar":12,"Foo":"c12"
            },
            {
                "Bar":13,"Foo":"c13"
            }
        ]
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json1,
            CancellationToken.None);

        var json2 =
            """
{
    "Id": 2,
    "$type": "MyEntity",
    "id": "2",
    "Json": {
        "Collection":
        [
            {
                "Bar":21,"Foo":"c21"
            },
            {
                "Bar":22,"Foo":"c22"
            }
        ]
    }
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            json2,
            CancellationToken.None);
    }

    [StoreDefect("1.24", UpstreamRepository.EfCore, Source, 3766, 3772, Skip = true, Justification = "missing collection comes back as empty on Cosmos")]

    // missing collection comes back as empty on Cosmos
    public override Task Bad_json_properties_empty_navigations(bool noTracking)
        => throw SkipException.ForSkip("missing collection comes back as empty on Cosmos");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3774, 3776, Skip = true, Justification = "Insertion of the bad data fails thanks to json validation by Cosmos DB")]
    // Insertion of the bad data fails thanks to json validation by Cosmos DB
    public override Task Bad_json_properties_null_navigations(bool noTracking)
       => throw SkipException.ForSkip("Insertion of the bad data fails thanks to json validation by Cosmos DB");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3778, 3779, Skip = true, Justification = "No reason given by upstream.")]
    public override Task Bad_json_properties_null_scalars(bool noTracking)
       => throw SkipException.ForSkip("No reason given by upstream.");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3781, 3783, Skip = true, Justification = "Insertion of the bad data is deduplicated by Cosmos DB")]
    // Insertion of the bad data is deduplicated by Cosmos DB
    public override Task Bad_json_properties_duplicated_navigations(bool noTracking)
        => throw SkipException.ForSkip("Insertion of the bad data is deduplicated by Cosmos DB");

    [StoreLimit(UpstreamRepository.EfCore, Source, 3785, 3786, Skip = true, Justification = "No reason given by upstream.")]
    public override Task Bad_json_properties_duplicated_scalars(bool noTracking)
        => throw SkipException.ForSkip("No reason given by upstream.");

    protected override void OnModelCreatingBadJsonProperties(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingBadJsonProperties(modelBuilder);

        modelBuilder.Entity<ContextBadJsonProperties.Entity>().ToContainer("Entities");
    }

    protected override async Task SeedBadJsonProperties(ContextBadJsonProperties context)
    {
        var wrapper = (CosmosClientWrapper)context.GetService<ICosmosClientWrapper>();
        var singletonWrapper = context.GetService<ISingletonCosmosClientWrapper>();
        var entitiesContainer = singletonWrapper.Client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var baseline =
            """
{
    "Id": 1,
    "$type": "Entity",
    "id": "1",
    "Scenario": "baseline",
    "OptionalReference": {"NestedOptional": { "Text":"or no" }, "NestedRequired": { "Text":"or nr" }, "NestedCollection": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ] },
    "RequiredReference": {"NestedOptional": { "Text":"rr no" }, "NestedRequired": { "Text":"rr nr" }, "NestedCollection": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ] },
    "Collection":
    [
        {"NestedOptional": { "Text":"c 1 no" }, "NestedRequired": { "Text":"c 1 nr" }, "NestedCollection": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ] },
        {"NestedOptional": { "Text":"c 2 no" }, "NestedRequired": { "Text":"c 2 nr" }, "NestedCollection": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ] }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            baseline,
            CancellationToken.None);

        var duplicatedNavigations =
            """
{
    "Id": 2,
    "$type": "Entity",
    "id": "2",
    "Scenario": "duplicated navigations",
    "OptionalReference": {"NestedOptional": { "Text":"or no" }, "NestedOptional": { "Text":"or no dupnav" }, "NestedRequired": { "Text":"or nr" }, "NestedCollection": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ], "NestedCollection": [ { "Text":"or nc 1 dupnav" }, { "Text":"or nc 2 dupnav" } ], "NestedRequired": { "Text":"or nr dupnav" } },
    "RequiredReference": {"NestedOptional": { "Text":"rr no" }, "NestedOptional": { "Text":"rr no dupnav" }, "NestedRequired": { "Text":"rr nr" }, "NestedCollection": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ], "NestedCollection": [ { "Text":"rr nc 1 dupnav" }, { "Text":"rr nc 2 dupnav" } ], "NestedRequired": { "Text":"rr nr dupnav" } },
    "Collection":
    [
        {"NestedOptional": { "Text":"c 1 no" }, "NestedOptional": { "Text":"c 1 no dupnav" }, "NestedRequired": { "Text":"c 1 nr" }, "NestedCollection": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ], "NestedCollection": [ { "Text":"c 1 nc 1 dupnav" }, { "Text":"c 1 nc 2 dupnav" } ], "NestedRequired": { "Text":"c 1 nr dupnav" } },
        {"NestedOptional": { "Text":"c 2 no" }, "NestedOptional": { "Text":"c 2 no dupnav" }, "NestedRequired": { "Text":"c 2 nr" }, "NestedCollection": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ], "NestedCollection": [ { "Text":"c 2 nc 1 dupnav" }, { "Text":"c 2 nc 2 dupnav" } ], "NestedRequired": { "Text":"c 2 nr dupnav" } }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            duplicatedNavigations,
            CancellationToken.None);

        var duplicatedScalars =
            """
{
    "Id": 3,
    "$type": "Entity",
    "id": "3",
    "Scenario": "duplicated scalars",
    "OptionalReference": {"NestedOptional": { "Text":"or no", "Text":"or no dupprop" }, "NestedRequired": { "Text":"or nr", "Text":"or nr dupprop" }, "NestedCollection": [ { "Text":"or nc 1", "Text":"or nc 1 dupprop" }, { "Text":"or nc 2", "Text":"or nc 2 dupprop" } ] },
    "RequiredReference": {"NestedOptional": { "Text":"rr no", "Text":"rr no dupprop" }, "NestedRequired": { "Text":"rr nr", "Text":"rr nr dupprop" }, "NestedCollection": [ { "Text":"rr nc 1", "Text":"rr nc 1 dupprop" }, { "Text":"rr nc 2", "Text":"rr nc 2 dupprop" } ] },
    "Collection":
    [
        {"NestedOptional": { "Text":"c 1 no", "Text":"c 1 no dupprop" }, "NestedRequired": { "Text":"c 1 nr", "Text":"c 1 nr dupprop" }, "NestedCollection": [ { "Text":"c 1 nc 1", "Text":"c 1 nc 1 dupprop" }, { "Text":"c 1 nc 2", "Text":"c 1 nc 2 dupprop" } ] },
        {"NestedOptional": { "Text":"c 2 no", "Text":"c 2 no dupprop" }, "NestedRequired": { "Text":"c 2 nr", "Text":"c 2 nr dupprop" }, "NestedCollection": [ { "Text":"c 2 nc 1", "Text":"c 2 nc 1 dupprop" }, { "Text":"c 2 nc 2", "Text":"c 2 nc 2 dupprop" } ] }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            duplicatedScalars,
            CancellationToken.None);

        var emptyNavs =
            """
{
    "Id": 4,
    "$type": "Entity",
    "id": "4",
    "Scenario": "empty navigation property names",
    "OptionalReference": {"": { "Text":"or no" }, "": { "Text":"or nr" }, "": [ { "Text":"or nc 1" }, { "Text":"or nc 2" } ] },
    "RequiredReference": {"": { "Text":"rr no" }, "": { "Text":"rr nr" }, "": [ { "Text":"rr nc 1" }, { "Text":"rr nc 2" } ] },
    "Collection":
    [
        {"": { "Text":"c 1 no" }, "": { "Text":"c 1 nr" }, "": [ { "Text":"c 1 nc 1" }, { "Text":"c 1 nc 2" } ] },
        {"": { "Text":"c 2 no" }, "": { "Text":"c 2 nr" }, "": [ { "Text":"c 2 nc 1" }, { "Text":"c 2 nc 2" } ] }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            emptyNavs,
            CancellationToken.None);

        var emptyScalars =
            """
{
    "Id": 5,
    "$type": "Entity",
    "id": "5",
    "Scenario": "empty scalar property names",
    "OptionalReference": {"NestedOptional": { "":"or no" }, "NestedRequired": { "":"or nr" }, "NestedCollection": [ { "":"or nc 1" }, { "":"or nc 2" } ] },
    "RequiredReference": {"NestedOptional": { "":"rr no" }, "NestedRequired": { "":"rr nr" }, "NestedCollection": [ { "":"rr nc 1" }, { "":"rr nc 2" } ] },
    "Collection":
    [
        {"NestedOptional": { "":"c 1 no" }, "NestedRequired": { "":"c 1 nr" }, "NestedCollection": [ { "":"c 1 nc 1" }, { "":"c 1 nc 2" } ] },
        {"NestedOptional": { "":"c 2 no" }, "NestedRequired": { "":"c 2 nr" }, "NestedCollection": [ { "":"c 2 nc 1" }, { "":"c 2 nc 2" } ] }
    ]
}
""";

        await CreateCustomEntityHelperAsync(
            entitiesContainer,
            emptyScalars,
            CancellationToken.None);

        // Insertion of the bad data fails thanks to json validation by Cosmos DB
        // nullNavs or "Scenario": "null navigation property names" can not be tested
        // nullScalars or "Scenario": "null scalar property names" can not be tested
    }

    // TODO: This only asserts the current behavior of the provider, but the behavior is probably not ideal
    // Consider:
    // - Never storing foreign key properties in json, For projections when the foreign key is a member, we would need to translate to the owner id in the query pipeline
    // - Not allowing (explicit?) primary keys on embedded entity types
    // - Deprecating owned entity types as a whole

    // The workaround for old providers is:
    // For missing implicit PK properties on embedded collection entities, use ordinal

    [Theory, InlineData(true), InlineData(false)]
    public async Task Primary_key_baseline(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var entity = new ContextBadPrimaryKeyJsonProperties.Entity
                {
                    Id = 1,
                    Associate = new() { Id = 2 },
                    KeyedAssociate = new() { Id = 3 }, // Overwritten to 1 (entity id)
                    AssociateCollection =
                    [
                        new() { Id = 4 },
                        new() { Id = 5 }
                    ],
                    KeyedAssociateCollection =
                    [
                        new() { Id = 6 },
                        new() { Id = 7 }
                    ],
                    ForeignKeyAssociate = new() { Id = 8 },
                    KeyedForeignKeyAssociate = new() { Id = 9 },
                    ForeignKeyAssociateCollection =
                    [
                        new() { Id = 10 },
                        new() { Id = 11 }
                    ],
                    KeyedForeignKeyAssociateCollection =
                    [
                        new() { Id = 12 },
                        new() { Id = 13 }
                    ]
                };

                context.Add(entity);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            });

        using var context = contextFactory.CreateDbContext();
        using var direct = ((IInfoCarrierClientTestStore)NonSharedTestStore).Backend.CreateDbContext();
        var client = direct.Database.GetCosmosClient();
        var container = client.GetContainer(direct.Database.GetCosmosDatabaseId(), containerId: "Entities");

        var entityJson = JsonNode.Parse("""
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""")!.AsObject();

        var dbJson = JsonNode.Parse(
            new StreamReader(
                (await container.ReadItemStreamAsync("1", Microsoft.Azure.Cosmos.PartitionKey.None, cancellationToken: TestContext.Current.CancellationToken)).Content).ReadToEnd())!.AsObject();
        foreach (var property in dbJson.Where(x => x.Key.StartsWith("_")).ToList())
        {
            dbJson.Remove(property.Key);
        }

        var text = dbJson.ToString();

        Assert.True(JsonNode.DeepEquals(entityJson, dbJson));

        var entity = await (tracking ? context.Entities : context.Entities.AsNoTracking()).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, entity.Associate.Id);
        Assert.Equal(1, entity.KeyedAssociate.Id); // Is overwritten..

        Assert.Equal(4, entity.AssociateCollection.First().Id);
        Assert.Equal(6, entity.KeyedAssociateCollection.First().Id);

        Assert.Equal(8, entity.ForeignKeyAssociate.Id);
        Assert.Equal(9, entity.KeyedForeignKeyAssociate.Id);

        Assert.Equal(10, entity.ForeignKeyAssociateCollection.First().Id);
        Assert.Equal(12, entity.KeyedForeignKeyAssociateCollection.First().Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Root_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": null,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Root_missing_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(CoreStrings.InvalidKeyValue("Entity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Associate_null_non_primary_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": null
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, result.Associate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task Associate_missing_non_primary_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, result.Associate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociate_null_primary_key_uses_implicit(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {
    "Id": null
  },
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, result.KeyedAssociate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociate_incorrect_primary_key_uses_implicit(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {
    "Id": 99
  },
  "AssociateCollection": [
    {
      "Id": 4
    },
    {
      "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, result.KeyedAssociate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task AssociateCollection_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
      "Id": null
    },
    {
      "Id": null
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.AssociateCollection#AssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task AssociateCollection_missing_primary_key_doesnt_throw_and_uses_ordinal_workaround(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
    },
    {
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, result.AssociateCollection.First().Id);
        Assert.Equal(2, result.AssociateCollection.Last().Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociateCollection_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": null,
      "EntityId": 1
    },
    {
      "Id": null,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedAssociateCollection#AssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociateCollection_missing_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "EntityId": 1
    },
    {
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedAssociateCollection#AssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociateCollection_null_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": null
    },
    {
      "Id": 7,
      "EntityId": null
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (!tracking)
        {
            Assert.Equal(6, result.KeyedAssociateCollection.First().Id);
            Assert.Equal(7, result.KeyedAssociateCollection.Last().Id);
        }
        else
        {
            Assert.Empty(result.KeyedAssociateCollection);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociateCollection_missing_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6
    },
    {
      "Id": 7
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (!tracking)
        {
            Assert.Equal(6, result.KeyedAssociateCollection.First().Id);
            Assert.Equal(7, result.KeyedAssociateCollection.Last().Id);
        }
        else
        {
            Assert.Empty(result.KeyedAssociateCollection);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedAssociateCollection_incorrect_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 99
    },
    {
      "Id": 7,
      "EntityId": 99
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (!tracking)
        {
            Assert.Equal(6, result.KeyedAssociateCollection.First().Id);
            Assert.Equal(7, result.KeyedAssociateCollection.Last().Id);
        }
        else
        {
            Assert.Empty(result.KeyedAssociateCollection);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ForeignKeyAssociate_null_non_primary_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 3
    },
    {
        "Id": 4
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 5,
      "EntityId": 1
    },
    {
      "Id": 6,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": null
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ForeignKeyAssociate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ForeignKeyAssociate_missing_non_primary_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 3
    },
    {
        "Id": 4
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 5,
      "EntityId": 1
    },
    {
      "Id": 6,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();

        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(0, result.ForeignKeyAssociate.Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociate_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": null,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedForeignKeyAssociate#ForeignKeyAssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociate_missing_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedForeignKeyAssociate#ForeignKeyAssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociate_null_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": null
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Null(result.KeyedForeignKeyAssociate);
        }
        else
        {
            Assert.NotNull(result.KeyedForeignKeyAssociate);
            Assert.Equal(9, result.KeyedForeignKeyAssociate.Id);
            Assert.Equal(0, result.KeyedForeignKeyAssociate.EntityId);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociate_missing_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": null
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Null(result.KeyedForeignKeyAssociate);
        }
        else
        {
            Assert.NotNull(result.KeyedForeignKeyAssociate);
            Assert.Equal(9, result.KeyedForeignKeyAssociate.Id);
            Assert.Equal(0, result.KeyedForeignKeyAssociate.EntityId);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociate_incorrect_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 99
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Null(result.KeyedForeignKeyAssociate);
        }
        else
        {
            Assert.NotNull(result.KeyedForeignKeyAssociate);
            Assert.Equal(9, result.KeyedForeignKeyAssociate.Id);
            Assert.Equal(99, result.KeyedForeignKeyAssociate.EntityId);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ForeignKeyAssociateCollection_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": null
    },
    {
      "Id": null
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.ForeignKeyAssociateCollection#ForeignKeyAssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task ForeignKeyAssociateCollection_missing_primary_key_doesnt_throw_and_uses_ordinal_workaround(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
    },
    {
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 1
    },
    {
      "Id": 13,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, result.ForeignKeyAssociateCollection.First().Id);
        Assert.Equal(2, result.ForeignKeyAssociateCollection.Last().Id);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociateCollection_null_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": null,
      "EntityId": 1
    },
    {
      "Id": null,
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedForeignKeyAssociateCollection#ForeignKeyAssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociateCollection_missing_primary_key_throws(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "EntityId": 1
    },
    {
      "EntityId": 1
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SingleOrDefaultAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(CoreStrings.InvalidKeyValue("Entity.KeyedForeignKeyAssociateCollection#ForeignKeyAssociateEntity", "Id"), ex.Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociateCollection_null_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": null
    },
    {
      "Id": 13,
      "EntityId": null
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Empty(result.KeyedForeignKeyAssociateCollection);
        }
        else
        {
            foreach (var item in result.KeyedForeignKeyAssociateCollection)
            {
                Assert.Equal(0, item.EntityId);
            }
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociateCollection_missing_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12
    },
    {
      "Id": 13
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Empty(result.KeyedForeignKeyAssociateCollection);
        }
        else
        {
            foreach (var item in result.KeyedForeignKeyAssociateCollection)
            {
                Assert.Equal(0, item.EntityId);
            }
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public async Task KeyedForeignKeyAssociateCollection_incorrect_foreign_key_doesnt_throw(bool tracking)
    {
        var contextFactory = await InitializeNonSharedTest<ContextBadPrimaryKeyJsonProperties>(
            onConfiguring: b => b.ConfigureWarnings(ConfigureWarnings),
            seed: async context =>
            {
                var client = context.Database.GetCosmosClient();
                var container = client.GetContainer(context.Database.GetCosmosDatabaseId(), containerId: "Entities");

                var entity = """
{
  "$type": "Entity",
  "Id": 1,
  "id": "1",
  "Associate": {
    "Id": 2
  },
  "KeyedAssociate": {},
  "AssociateCollection": [
    {
        "Id": 4
    },
    {
        "Id": 5
    }
  ],
  "KeyedAssociateCollection": [
    {
      "Id": 6,
      "EntityId": 1
    },
    {
      "Id": 7,
      "EntityId": 1
    }
  ],
  "ForeignKeyAssociate": {
    "Id": 8
  },
  "KeyedForeignKeyAssociate": {
    "Id": 9,
    "EntityId": 1
  },
  "ForeignKeyAssociateCollection": [
    {
      "Id": 10
    },
    {
      "Id": 11
    }
  ],
  "KeyedForeignKeyAssociateCollection": [
    {
      "Id": 12,
      "EntityId": 99
    },
    {
      "Id": 13,
      "EntityId": 99
    }
  ]
}
""";
                await CreateCustomEntityHelperAsync(
                    container,
                    entity,
                    CancellationToken.None);
            });

        using var context = contextFactory.CreateDbContext();
        IQueryable<ContextBadPrimaryKeyJsonProperties.Entity> query = context.Entities;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var result = await query.SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
        if (tracking)
        {
            Assert.Empty(result.KeyedForeignKeyAssociateCollection);
        }
        else
        {
            foreach (var item in result.KeyedForeignKeyAssociateCollection)
            {
                Assert.Equal(99, item.EntityId);
            }
        }
    }

    private class ContextBadPrimaryKeyJsonProperties : DbContext
    {
        public ContextBadPrimaryKeyJsonProperties(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<Entity> Entities { get; set; } = null!;

        public class Entity
        {
            public int Id { get; set; }

            public AssociateEntity Associate { get; set; } = null!;
            public AssociateEntity KeyedAssociate { get; set; } = null!;

            public List<AssociateEntity> AssociateCollection { get; set; } = new();
            public List<AssociateEntity> KeyedAssociateCollection { get; set; } = new();

            public ForeignKeyAssociateEntity ForeignKeyAssociate { get; set; } = null!;
            public ForeignKeyAssociateEntity KeyedForeignKeyAssociate { get; set; } = null!;

            public List<ForeignKeyAssociateEntity> ForeignKeyAssociateCollection { get; set; } = new();
            public List<ForeignKeyAssociateEntity> KeyedForeignKeyAssociateCollection { get; set; } = new();
        }

        public class AssociateEntity
        {
            public int Id { get; set; }
        }

        public class ForeignKeyAssociateEntity
        {
            public int Id { get; set; }

            public int EntityId { get; set; }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<Entity>();
            entity.ToContainer("Entities");
            entity.OwnsOne(x => x.Associate);
            entity.OwnsOne(x => x.KeyedAssociate, b => b.HasKey(x => x.Id));
            entity.OwnsMany(x => x.AssociateCollection);
            entity.OwnsMany(x => x.KeyedAssociateCollection, b => b.HasKey(x => x.Id));

            entity.OwnsOne(x => x.ForeignKeyAssociate);
            entity.OwnsOne(x => x.KeyedForeignKeyAssociate, b => b.HasKey(x => x.Id));
            entity.OwnsMany(x => x.ForeignKeyAssociateCollection);
            entity.OwnsMany(x => x.KeyedForeignKeyAssociateCollection, b => b.HasKey(x => x.Id));
        }
    }


}
