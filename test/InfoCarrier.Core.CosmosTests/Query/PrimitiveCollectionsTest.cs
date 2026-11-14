// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.CosmosTests.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.CosmosTests.Query;

public class PrimitiveCollectionsTest(PrimitiveCollectionsTest.CosmosFixture fixture)
    : PrimitiveCollectionsQueryTestBase<PrimitiveCollectionsTest.CosmosFixture>(fixture)
{
    private const string Source = "test/EFCore.Cosmos.FunctionalTests/Query/PrimitiveCollectionsQueryCosmosTest.cs";

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

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 58, 74,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Count_with_zero_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 76, 92,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Count_with_one_value() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 94, 110,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Count_with_two_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 112, 128,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Count_with_three_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 254, 269,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Min_with_two_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 271, 286,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_List_Min_with_two_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 288, 303,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Max_with_two_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 305, 320,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_List_Max_with_two_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 322, 339,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Min_with_three_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 341, 358,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_List_Min_with_three_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 360, 377,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Max_with_three_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 379, 396,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_List_Max_with_three_values() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 398, 415,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_of_nullable_value_type_Min() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 417, 434,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_of_nullable_value_type_Max() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("1.17", UpstreamRepository.EfCore, Source, 436, 453,
        Justification = "Cosmos MIN()/MAX() sort nulls as smaller than ints.")]
    public override async Task Inline_collection_of_nullable_value_type_with_null_Min()
    {
        // Cosmos MIN()/MAX() sort nulls as smaller than ints (https://learn.microsoft.com/azure/cosmos-db/nosql/query/min);
        // since some of the columns included contain null, MIN() returns null as opposed to the smallest number.
        // In relational, aggregate MIN()/MAX() ignores nulls.
        await Assert.ThrowsAsync<EqualException>(base.Inline_collection_of_nullable_value_type_with_null_Min);
    }

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 455, 472,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_of_nullable_value_type_with_null_Max() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 484, 502,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_with_single_parameter_element_Count() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 532, 550,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_Count_with_column_predicate_with_EF_Parameter() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 552, 569,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Inline_collection_in_query_filter() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 585, 603,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Parameter_collection_Count() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreLimit(UpstreamRepository.EfCore, Source, 817, 823,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_structs_Contains_struct()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_of_structs_Contains_struct);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 825, 831,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_structs_Contains_nullable_struct()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_of_structs_Contains_nullable_struct);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 833, 840,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_of_structs_Contains_nullable_struct_with_nullable_comparer);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 842, 848,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_nullable_structs_Contains_struct()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_of_nullable_structs_Contains_struct);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 850, 856,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_nullable_structs_Contains_nullable_struct()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_of_nullable_structs_Contains_nullable_struct);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 858, 865,
        Justification = "Requires collections of converted elements")]
    public override async Task Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer()
    {
        // Requires collections of converted elements
        await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_of_nullable_structs_Contains_nullable_struct_with_nullable_comparer);
    }

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 1025, 1031,
        Justification = "Cosmos join support. Issue #16920.")]
    public override async Task Parameter_collection_empty_Join()
    {
        // Cosmos join support. Issue #16920.
        await AssertTranslationFailed(base.Parameter_collection_empty_Join);
    }

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 1033, 1038,
        Justification = "#34327")]
    public override async Task Parameter_collection_Contains_with_EF_Constant()
    {
        // #34327
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_Contains_with_EF_Constant);
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 1040, 1045,
        Justification = "#34327")]
    public override async Task Parameter_collection_Where_with_EF_Constant_Where_Any()
    {
        // #34327
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(base.Parameter_collection_Where_with_EF_Constant_Where_Any);
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 34327, UpstreamRepository.EfCore, Source, 1047, 1053,
        Justification = "#34327")]
    public override async Task Parameter_collection_Count_with_column_predicate_with_EF_Constant()
    {
        // #34327
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_Count_with_column_predicate_with_EF_Constant);
        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 34026, UpstreamRepository.EfCore, Source, 1263, 1269,
        Justification = "TODO: advanced type mapping inference for inline scalar collection, #34026")]
    public override async Task Constant_with_inferred_value_converter()
    {
        // TODO: advanced type mapping inference for inline scalar collection, #34026
        await AssertTranslationFailed(() => base.Constant_with_inferred_value_converter());
    }

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 1311, 1326,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Min_on_MemoryExtensions() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 1328, 1343,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Max_on_MemoryExtensions() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 1369, 1385,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Column_collection_Count_with_predicate() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 1387, 1403,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Column_collection_Where_Count() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreDefect("1.18", UpstreamRepository.EfCore, Source, 1453, 1463,
        Justification = Upstream.GaveNoReason)]
    public override async Task Nullable_reference_column_collection_index_equals_nullable_column()
    {
        await Assert.ThrowsAsync<EqualException>(base.Nullable_reference_column_collection_index_equals_nullable_column);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1477, 1490,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Inline_collection_index_Column()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Inline_collection_index_Column);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1492, 1497,
        Justification = Upstream.GaveNoReason)]
    public override async Task Inline_collection_index_Column_with_EF_Constant()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => base.Inline_collection_index_Column_with_EF_Constant());

        Assert.Equal(CoreStrings.EFConstantNotSupported, exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1499, 1512,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Inline_collection_value_index_Column()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Inline_collection_value_index_Column);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1514, 1527,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Inline_collection_List_value_index_Column()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Inline_collection_List_value_index_Column);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1529, 1544,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Parameter_collection_index_Column_equal_Column()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Parameter_collection_index_Column_equal_Column);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1546, 1561,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Parameter_collection_index_Column_equal_constant()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Parameter_collection_index_Column_equal_constant);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1719, 1735,
        Justification = "'ORDER BY' is not supported in subqueries.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Column_collection_OrderByDescending_ElementAt()
    {
        // 'ORDER BY' is not supported in subqueries.
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Column_collection_OrderByDescending_ElementAt);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreIssue(IssueTracker.EfCore, 33968, UpstreamRepository.EfCore, Source, 1764, 1770,
        Justification = "TODO: Subquery pushdown, #33968")]
    public override async Task Column_collection_Distinct()
    {
        // TODO: Subquery pushdown, #33968
        await AssertTranslationFailed(base.Column_collection_Distinct);
    }

    [StoreIssue(IssueTracker.EfCore, 34004, UpstreamRepository.EfCore, Source, 1799, 1806,
        Justification = "TODO: #34004")]
    public override async Task Column_collection_SelectMany_with_Select_to_anonymous_type()
    {
        // TODO: #34004
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            base.Column_collection_SelectMany_with_Select_to_anonymous_type);

        Assert.Equal("Complex projections in subqueries are currently unsupported.", exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 1820, 1826,
        Justification = "Cosmos join support. Issue #16920.")]
    public override async Task Column_collection_Join_parameter_collection()
    {
        // Cosmos join support. Issue #16920.
        await AssertTranslationFailed(base.Column_collection_Join_parameter_collection);
    }

    [StoreIssue(IssueTracker.EfCore, 16920, UpstreamRepository.EfCore, Source, 1828, 1834,
        Justification = "Cosmos join support. Issue #16920.",
        Deviation = DeviationKind.UpstreamCallsAnotherTest,
        DeviationNote = "Call the matching base test; upstream calls Column_collection_Join_parameter_collection.")]
    public override async Task Inline_collection_Join_ordered_column_collection()
    {
        // Cosmos join support. Issue #16920.
        await AssertTranslationFailed(base.Inline_collection_Join_ordered_column_collection);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1850, 1865,
        Justification = "Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Parameter_collection_with_type_inference_for_JsonScalarExpression()
    {
        // Member indexer (c.Array[c.SomeMember]) isn't supported by Cosmos
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(
            base.Parameter_collection_with_type_inference_for_JsonScalarExpression);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 1893, 1898,
        Justification = Upstream.GaveNoReason)]
    public override async Task Inline_collection_Except_column_collection()
    {
        await AssertTranslationFailedWithDetails(base.Inline_collection_Except_column_collection, "The 'Except()' LINQ operator isn't supported by Cosmos.");
    }

    [StoreIssue(IssueTracker.EfCore, 33931, UpstreamRepository.EfCore, Source, 1985, 1997,
        Justification = "TODO: #33931")]
    public override async Task Parameter_collection_in_subquery_Union_column_collection_as_compiled_query()
    {
        // TODO: #33931
        // The ToList inside the query gets executed separately during shaper generation - and synchronously (even in the async
        // variant of the test), but Cosmos doesn't support sync I/O. So both sync and async variants fail because of unsupported
        // sync I/O.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_in_subquery_Union_column_collection_as_compiled_query);

        Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2013, 2019,
        Justification = "Subquery pushdown")]
    public override async Task Parameter_collection_in_subquery_Union_column_collection_nested()
    {
        // TODO: Subquery pushdown
        await AssertTranslationFailed(base.Parameter_collection_in_subquery_Union_column_collection_nested);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2021, 2028,
        Justification = "Array indexer over a parameter array ([1,2,3][0]) isn't supported by Cosmos.")]
    public override void Parameter_collection_in_subquery_and_Convert_as_compiled_query()
    {
        // Array indexer over a parameter array ([1,2,3][0]) isn't supported by Cosmos.
        // TODO: general OFFSET/LIMIT support
        AssertTranslationFailed(base.Parameter_collection_in_subquery_and_Convert_as_compiled_query);
    }

    [StoreIssue(IssueTracker.EfCore, 33931, UpstreamRepository.EfCore, Source, 2030, 2042,
        Justification = "TODO: #33931")]
    public override async Task Parameter_collection_in_subquery_Count_as_compiled_query()
    {
        // TODO: #33931
        // The ToList inside the query gets executed separately during shaper generation - and synchronously (even in the async
        // variant of the test), but Cosmos doesn't support sync I/O. So both sync and async variants fail because of unsupported
        // sync I/O.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_in_subquery_Count_as_compiled_query);

        Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", exception.Message);
    }

    [StoreIssue(IssueTracker.EfCore, 33931, UpstreamRepository.EfCore, Source, 2044, 2056,
        Justification = "TODO: #33931")]
    public override async Task Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query()
    {
        // TODO: #33931
        // The ToList inside the query gets executed separately during shaper generation - and synchronously (even in the async
        // variant of the test), but Cosmos doesn't support sync I/O. So both sync and async variants fail because of unsupported
        // sync I/O.
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            base.Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query);

        Assert.Equal("Azure Cosmos DB does not support synchronous I/O. Make sure to use and correctly await only async methods when using Entity Framework Core to access Azure Cosmos DB.", exception.Message);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2098, 2114,
        Justification = "'ORDER BY' is not supported in subqueries.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Project_collection_of_ints_ordered()
    {
        // 'ORDER BY' is not supported in subqueries.
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Project_collection_of_ints_ordered);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2145, 2161,
        Justification = "'ORDER BY' is not supported in subqueries.",
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Project_collection_of_nullable_ints_with_paging2()
    {
        // 'ORDER BY' is not supported in subqueries.
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Project_collection_of_nullable_ints_with_paging2);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2238, 2267,
        Justification = Upstream.GaveNoReason,
        Deviation = DeviationKind.StoreExceptionAsData,
        DeviationNote = "Assert the remote CosmosException type and its BadRequest status text.")]
    public override async Task Project_multiple_collections()
    {
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(base.Project_multiple_collections);

        Assert.Equal(typeof(Microsoft.Azure.Cosmos.CosmosException).FullName, exception.ServerExceptionTypeName.Split(',')[0]);
        Assert.Contains("BadRequest (400)", exception.Message, StringComparison.Ordinal);
    }

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 2269, 2288,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Project_primitive_collections_element() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    [StoreIssue(IssueTracker.EfCore, 33779, UpstreamRepository.EfCore, Source, 2290, 2303,
        Justification = "The following should be SELECT VALUE [c[\"String\"], \"foo\"], #33779")]
    public override async Task Project_inline_collection()
    {
        await AssertQuery(
            ss => ss.Set<PrimitiveCollectionsEntity>().Select(x => new[] { x.String, "foo" }),
            elementAsserter: (e, a) => AssertCollection(e, a, ordered: true),
            elementSorter: e => e[0]);

        // The following should be SELECT VALUE [c["String"], "foo"], #33779
    }

    [StoreLimit(UpstreamRepository.EfCore, Source, 2305, 2307,
        Justification = "Non-correlated queries not supported by Cosmos")]
    // Non-correlated queries not supported by Cosmos
    public override Task Project_inline_collection_with_Union()
        => AssertTranslationFailed(base.Project_inline_collection_with_Union);

    [StoreLimit(UpstreamRepository.EfCore, Source, 2309, 2311,
        Justification = "Non-correlated queries not supported by Cosmos")]
    // Non-correlated queries not supported by Cosmos
    public override Task Project_inline_collection_with_Concat()
        => AssertTranslationFailed(base.Project_inline_collection_with_Concat);

    [StoreDefect("2.1", UpstreamRepository.EfCore, Source, 2367, 2383,
        Skip = true, Justification = "Aggregates over subqueries return null result set")]
    public override Task Values_of_enum_casted_to_underlying_value() => throw SkipException.ForSkip("Not supported on the Linux Cosmos emulator");

    // Additional official Cosmos behavior, source lines 2385-2400.
    [Fact]
    public virtual async Task IsDefined()
    {
        await AssertQuery(
            ss => ss.Set<PrimitiveCollectionsEntity>().Where(e => EF.Functions.IsDefined(e.Ints[2])),
            ss => ss.Set<PrimitiveCollectionsEntity>().Where(e => e.Ints.Length >= 3));
    }

    // Additional official Cosmos behavior, source lines 2402-2415.
    [Fact]
    public virtual async Task CoalesceUndefined()
    {
        await AssertQuery(
            ss => ss.Set<PrimitiveCollectionsEntity>().Where(e => EF.Functions.CoalesceUndefined(e.Ints[2], 999) == 999),
            ss => ss.Set<PrimitiveCollectionsEntity>().Where(e => e.Ints.Length < 3));
    }

    public class CosmosFixture : PrimitiveCollectionsQueryFixtureBase
    {
        private ITestStoreFactory? _factory;

        protected override ITestStoreFactory TestStoreFactory => _factory ??= InfoCarrierTestStoreFactory.Create(
            new CosmosTier(), ContextType, OnModelCreating,
            configureConventions: ConfigureConventions,
            allowedTypes:
            [
                typeof(CosmosDbFunctionsExtensions),
                typeof(PrimitiveCollectionsQueryTestBase<>).Assembly
                    .GetType("Microsoft.EntityFrameworkCore.Query.ReadOnlyCollectionWithContains`1")!
                    .MakeGenericType(typeof(int)),
            ],
            configureServer: options => options.WithStoresOwnedDataInsideOwner(true));

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base.AddOptions(builder.ConfigureWarnings(warnings => warnings.Ignore(CosmosEventId.NoPartitionKeyDefined)));

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);
            // Official Cosmos fixture: requires element type mapping, issue 34026.
            modelBuilder.Entity<PrimitiveCollectionsEntity>().Ignore(entity => entity.Enums);
        }
    }
}
