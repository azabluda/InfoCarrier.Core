// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>PrimitiveCollectionsQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     The shared-model half of the primitive-collection corpus: one entity carrying an
///     <c>int[]</c>, a <c>List&lt;string&gt;</c>, a <c>DateTime[]</c> and their nullable twins,
///     queried through every operator a collection supports. <b>Tier B</b> for the same reason as
///     <see cref="NonSharedPrimitiveCollectionsQuerySqliteInfoCarrierTest" /> — EF ships
///     <c>PrimitiveCollectionsQuerySqliteTest</c> and no InMemory counterpart, because a primitive
///     collection is a thing a store either maps or does not.
/// </remarks>
public class PrimitiveCollectionsQuerySqliteInfoCarrierTest(
    PrimitiveCollectionsQuerySqliteInfoCarrierTest.PrimitiveCollectionsQuerySqliteInfoCarrierFixture fixture)
    : PrimitiveCollectionsQueryTestBase<
        PrimitiveCollectionsQuerySqliteInfoCarrierTest.PrimitiveCollectionsQuerySqliteInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The thirteen overrides below are EF's own, from <c>PrimitiveCollectionsQuerySqliteTest</c>.
    /// </summary>
    /// <remarks>
    ///     Each is a query that now reaches SQL and asks SQLite for <c>APPLY</c>, which it does not
    ///     have. That is convergence with the reference provider, not a defect of this one — the
    ///     rule CLAUDE.md states for a newly-red SQLite test. EF overrides a fourteenth,
    ///     <c>Project_collection_of_nullable_ints_with_distinct</c>, which is skipped here.
    /// </remarks>
    public override async Task Column_collection_SelectMany()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Column_collection_SelectMany_with_filter()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_filter());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Column_collection_SelectMany_with_Select_to_anonymous_type()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_Select_to_anonymous_type());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_datetimes_filtered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_datetimes_filtered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_ordered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_ordered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_with_ToList_and_FirstOrDefault()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_ToList_and_FirstOrDefault());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_ints_with_distinct()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_distinct());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging2()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging2());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_collection_of_nullable_ints_with_paging3()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging3());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => await AssertApplyNotSupported(
            () => base.Project_empty_collection_of_nullables_and_collection_only_containing_nulls());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_inline_collection_with_Union()
        => await AssertApplyNotSupported(() => base.Project_inline_collection_with_Union());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    public override async Task Project_multiple_collections()
        => await AssertApplyNotSupported(() => base.Project_multiple_collections());

    /// <summary>
    ///     Four more of EF's own, from the same class, for a different SQLite limitation.
    /// </summary>
    /// <remarks>
    ///     Indexing an inline collection by a column puts that column in the correlated subquery's
    ///     <c>OFFSET</c>, which SQLite refuses — <c>no such column: "p"."Int"</c>. EF asserts the
    ///     raw <see cref="SqliteException" /> here rather than overriding the query, and so did we
    ///     until W5 (plan C46): the exception is the engine's, raised at the same place for the
    ///     same reason, which is what makes it convergence rather than a borrowed excuse. What
    ///     changed is only how it reaches this client — see <see cref="AssertStoreRefuses" />.
    ///     <para>
    ///         Note what is <em>not</em> taken. EF overrides the two
    ///         <c>Parameter_collection_index_Column_*</c> tests too, but by calling
    ///         <c>base</c> — they pass there, because a parameter reaches SQL as a JSON string and
    ///         is indexed with <c>-&gt;&gt;</c> rather than through a subquery. They fail here for a
    ///         reason of ours (B14), so they stay red.
    ///     </para>
    /// </remarks>
    public override Task Inline_collection_index_Column()
        => AssertStoreRefuses(base.Inline_collection_index_Column);

    /// <inheritdoc cref="Inline_collection_index_Column" />
    public override Task Inline_collection_value_index_Column()
        => AssertStoreRefuses(base.Inline_collection_value_index_Column);

    /// <inheritdoc cref="Inline_collection_index_Column" />
    public override Task Inline_collection_List_value_index_Column()
        => AssertStoreRefuses(base.Inline_collection_List_value_index_Column);

    /// <summary>
    ///     The same refusal EF asserts, described the way a <em>remoting</em> client can see it
    ///     (wire-protocol W5, plan C46).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         These three used to assert <see cref="SqliteException" /> directly, and that passed
    ///         only because client and server share a process: the store's exception reached the
    ///         caller by propagating, as the same object. Once a failure crosses as data, it
    ///         cannot — <see cref="SqliteException" /> has no message-and-inner constructor to be
    ///         rebuilt through, and more fundamentally **a client has no reason to reference the
    ///         backend's driver at all**. That is a property of remoting, not a defect.
    ///     </para>
    ///     <para>
    ///         So the assertion moves to what actually crosses, and it is <em>stronger</em> than
    ///         what it replaces: the engine still refuses the query at the same place, and the
    ///         type name and the engine's own message both survive the wire intact. Asserting
    ///         only <see cref="InfoCarrierServerException" /> would have been weaker; asserting
    ///         the name and the message is not.
    ///     </para>
    ///     <para>
    ///         The base test still runs, still reaches SQLite, and still fails there. Nothing is
    ///         suppressed.
    ///     </para>
    /// </remarks>
    private static async Task AssertStoreRefuses(Func<Task> query)
    {
        var exception = await Assert.ThrowsAsync<InfoCarrierServerException>(query);

        Assert.Equal(typeof(SqliteException).FullName, exception.ServerExceptionTypeName);
        Assert.Contains("no such column", exception.Message);
    }

    /// <inheritdoc />
    /// <remarks>
    ///     EF's own override, for EF issue #32561: concatenating a parameter collection onto a
    ///     column collection returns the wrong rows on SQLite, and EF asserts the mismatch rather
    ///     than the result. Ours mismatches identically — an <see cref="EqualException" /> out of
    ///     the same assertion — so the override transfers.
    /// </remarks>
    public override async Task Parameter_collection_Concat_column_collection()
        => await Assert.ThrowsAsync<EqualException>(() => base.Parameter_collection_Concat_column_collection());

    /// <summary>
    ///     Four from <c>PrimitiveCollectionsQueryRelationalTestBase</c>, which this project does not
    ///     reference and so must mirror by hand.
    /// </summary>
    /// <remarks>
    ///     Each is a limit <em>every</em> relational provider has, which is why EF states it on the
    ///     relational base rather than in SQLite's own suite — and why reading only the latter left
    ///     all four classified as failures of this provider. The reason has to match, not just the
    ///     name (A63): an empty inline collection and a <c>Concat</c> in a projection each raise the
    ///     exact <c>RelationalStrings</c> message EF asserts, and the two inline-collection equality
    ///     comparisons fail translation for the reason EF's TODO gives — comparing a relational
    ///     rowset against a primitive collection, EF issue #33792.
    /// </remarks>
    public override async Task Inline_collection_Count_with_zero_values()
        => Assert.Equal(
            RelationalStrings.EmptyCollectionNotSupportedAsInlineQueryRoot,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Inline_collection_Count_with_zero_values())).Message);

    /// <inheritdoc cref="Inline_collection_Count_with_zero_values" />
    public override async Task Project_inline_collection_with_Concat()
        => Assert.Equal(
            RelationalStrings.InsufficientInformationToIdentifyElementOfCollectionJoin,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Project_inline_collection_with_Concat())).Message);

    /// <inheritdoc cref="Inline_collection_Count_with_zero_values" />
    public override async Task Column_collection_Concat_parameter_collection_equality_inline_collection()
        => await AssertTranslationFailed(
            () => base.Column_collection_Concat_parameter_collection_equality_inline_collection());

    /// <inheritdoc cref="Inline_collection_Count_with_zero_values" />
    public override async Task Column_collection_Where_equality_inline_collection()
        => await AssertTranslationFailed(() => base.Column_collection_Where_equality_inline_collection());

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    public class PrimitiveCollectionsQuerySqliteInfoCarrierFixture : PrimitiveCollectionsQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "PrimitiveCollectionsQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
