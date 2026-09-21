// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;
using Xunit.Sdk;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

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
///     <para>
///         <b>R31 re-parented this onto <c>PrimitiveCollectionsQueryRelationalTestBase</c></b>, and
///         four hand-mirrored overrides went with it. The remark they carried — "which this
///         project does not reference and so must mirror by hand" — stopped being true when
///         ADR-013 admitted <c>EFCore.Relational.Specification.Tests</c>. <b>The fixture does not
///         move</b>: that base constrains <c>TFixture</c> to the <em>core</em>
///         <c>PrimitiveCollectionsQueryFixtureBase</c> and calls no <c>AssertSql</c>, so there is
///         nothing for a relational fixture to supply.
///     </para>
/// </remarks>
public class PrimitiveCollectionsQuerySqliteInfoCarrierTest(
    PrimitiveCollectionsQuerySqliteInfoCarrierTest.PrimitiveCollectionsQuerySqliteInfoCarrierFixture fixture)
    : PrimitiveCollectionsQueryRelationalTestBase<
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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1398, 1401,
        Justification = Upstream.GaveNoReason)]
    public override async Task Column_collection_SelectMany()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1403, 1406,
        Justification = Upstream.GaveNoReason)]
    public override async Task Column_collection_SelectMany_with_filter()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_filter());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1408, 1412,
        Justification = Upstream.GaveNoReason)]
    public override async Task Column_collection_SelectMany_with_Select_to_anonymous_type()
        => await AssertApplyNotSupported(() => base.Column_collection_SelectMany_with_Select_to_anonymous_type());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1802, 1805,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_datetimes_filtered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_datetimes_filtered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1797, 1800,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_ints_ordered()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_ordered());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1832, 1836,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_ints_with_ToList_and_FirstOrDefault()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_ToList_and_FirstOrDefault());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1822, 1825,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_ints_with_distinct()
        => await AssertApplyNotSupported(() => base.Project_collection_of_ints_with_distinct());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1807, 1810,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_nullable_ints_with_paging()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1812, 1815,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_nullable_ints_with_paging2()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging2());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1817, 1820,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_collection_of_nullable_ints_with_paging3()
        => await AssertApplyNotSupported(() => base.Project_collection_of_nullable_ints_with_paging3());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1879, 1883,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_empty_collection_of_nullables_and_collection_only_containing_nulls()
        => await AssertApplyNotSupported(
            () => base.Project_empty_collection_of_nullables_and_collection_only_containing_nulls());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1867, 1870,
        Justification = Upstream.GaveNoReason)]
    public override async Task Project_inline_collection_with_Union()
        => await AssertApplyNotSupported(() => base.Project_inline_collection_with_Union());

    /// <inheritdoc cref="Column_collection_SelectMany" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1838, 1841,
        Justification = Upstream.GaveNoReason)]
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
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1040, 1055,
        Justification = "SQLite doesn't support correlated subqueries where the outer column is used as the LIMIT/OFFSET (see OFFSET \"p\".\"Int\" below)",
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Inline_collection_index_Column()
        => AssertStoreRefuses(base.Inline_collection_index_Column);

    /// <inheritdoc cref="Inline_collection_index_Column" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1069, 1084,
        Justification = "SQLite doesn't support correlated subqueries where the outer column is used as the LIMIT/OFFSET (see OFFSET \"p\".\"Int\" below)",
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Inline_collection_value_index_Column()
        => AssertStoreRefuses(base.Inline_collection_value_index_Column);

    /// <inheritdoc cref="Inline_collection_index_Column" />
    [StoreLimit(
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1086, 1101,
        Justification = "SQLite doesn't support correlated subqueries where the outer column is used as the LIMIT/OFFSET (see OFFSET \"p\".\"Int\" below)",
        Deviation = DeviationKind.StoreExceptionAsData | DeviationKind.SqlNotAsserted)]
    public override Task Inline_collection_List_value_index_Column()
        => AssertStoreRefuses(base.Inline_collection_List_value_index_Column);

    /// <summary>
    ///     A compiled query EF's relational base asserts a refusal for, and this provider answers.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>The store reason and this provider's reason are two attributes</b> (2026-09-15).
    ///         <c>PrimitiveCollectionsQueryRelationalTestBase</c> wraps it in a refusal, because EF's
    ///         relational pipeline leaves a compiled query's parameter inside a subquery without a
    ///         type mapping, and EF's own comment says so outright: <c>docs/upstream-defects.md</c>
    ///         §1.12. <b>This provider does not reach that state, and the reason is measured</b>: the
    ///         client sends a compiled query's parameters as values (ADR-006), and
    ///         <c>parameters[0]</c> is an array index rather than an operator the client marks, so
    ///         the server's EF evaluates it before translation and runs
    ///         <c>WHERE "p"."String" = @p</c>.
    ///     </para>
    ///     <para>
    ///         <b>This was two tests until 2026-09-20.</b>
    ///         <c>Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query</c>
    ///         answered for the same reason, with <c>WHERE @p</c>, until #122 made the client mark
    ///         <c>ints1.Skip(1)</c> as EF keeps it. It now raises EF's own
    ///         <c>SetOperationsRequireAtLeastOneSideWithValidTypeMapping</c>, and its override went.
    ///         This remark said "two" until 2026-09-22.
    ///     </para>
    ///     <para>
    ///         <b>The query body is EF's core one, copied</b>, because C# cannot call a grandparent's
    ///         implementation and the relational base sits between, and it asserts the rows where the
    ///         core body asserts nothing. The copy is the real cost: an edit to EF's base will not
    ///         reach it.
    ///     </para>
    ///     <para>
    ///         <b>There were three until 2026-09-15.</b>
    ///         <c>Column_collection_equality_inline_collection_with_parameters</c> answered too, because
    ///         a scalar inside <c>new[] { i, j }</c> crossed as a literal, and the same rule turned
    ///         <c>new[] { i, j }.Contains(p.Id)</c> into <c>IN (2, 999)</c> where EF sends
    ///         <c>IN (@i, @j)</c>. That was a defect and is fixed in <c>Substitute</c>; the test now
    ///         inherits EF's refusal and passes with it.
    ///     </para>
    /// </remarks>
    [StoreDefect(
        "1.12",
        UpstreamRepository.EfCore, "test/EFCore.Relational.Specification.Tests/Query/PrimitiveCollectionsQueryRelationalTestBase.cs", 25, 36,
        Justification = "The array indexing is translated as a subquery over e.g. OPENJSON with LIMIT/OFFSET. Since there's a "
            + "CAST over that, the type mapping inference from the other side (p.String) doesn't propagate inside to the "
            + "subquery. In this case, the CAST operand gets the default CLR type mapping, but that's object in this case. "
            + "We should apply the default type mapping to the parameter, but need to figure out the exact rules when to do this.")]
    [InfoCarrierDesign(
        6,
        Justification = "A compiled query's parameters cross as values, so the server's EF evaluates (string)parameters[0] before "
            + "translation and runs WHERE \"p\".\"String\" = @p. The indexed subquery EF cannot type is never built.",
        Deviation = DeviationKind.AnswerNotRefusal | DeviationKind.QueryWrittenOut,
        DeviationNote = "EF's core body, with the rows asserted.")]
    public override void Parameter_collection_in_subquery_and_Convert_as_compiled_query()
    {
        var query = EF.CompileQuery(
            (PrimitiveCollectionsContext context, object[] parameters)
                => context.Set<PrimitiveCollectionsEntity>().Where(p => p.String == (string)parameters[0]));

        using PrimitiveCollectionsContext context = Fixture.CreateContext();

        // EF's core body ends at `.ToList()` and asserts nothing about the rows. Asserting them is
        // the whole point of taking the override, so the predicate is checked here.
        Assert.All(query(context, ["foo"]).ToList(), e => Assert.Equal("foo", e.String));
    }

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
    [StoreIssue(
        IssueTracker.EfCore, 32561,
        UpstreamRepository.EfCore, "test/EFCore.Sqlite.FunctionalTests/Query/PrimitiveCollectionsQuerySqliteTest.cs", 1459, 1484,
        Justification = "Issue #32561",
        Deviation = DeviationKind.SqlNotAsserted)]
    public override async Task Parameter_collection_Concat_column_collection()
        => await Assert.ThrowsAsync<EqualException>(() => base.Parameter_collection_Concat_column_collection());

    // R31 deleted four overrides here. They were `PrimitiveCollectionsQueryRelationalTestBase`'s,
    // mirrored by hand because the test project did not then reference the relational
    // specification assembly, and the re-parent now inherits them verbatim -- along with three
    // more of that base's that this file never carried.
    //
    // THOSE THREE WERE LEFT FAILING, AND THEY FAILED BECAUSE THEY PASS. Each asserts that translation
    // must fail, and here it did not: "Assert.Throws() Failure: No exception was thrown". They were
    // overridden since V12 to assert the rows, and carry docs/upstream-defects.md 1.12 since
    // 2026-09-15.
    //
    //   Parameter_collection_in_subquery_and_Convert_as_compiled_query, still overridden
    //   Parameter_collection_in_subquery_Union_another_parameter_collection_as_compiled_query,
    //     inherited again since 2026-09-20 (#122), when the client began to mark `ints1.Skip(1)`
    //     as EF keeps it; it passes with EF's refusal
    //   Column_collection_equality_inline_collection_with_parameters, inherited again since
    //     2026-09-15, when the literal it answered through was fixed; it passes with EF's refusal
    //
    // All three are the same defect on EF's side and EF says so in its own TODO on the first:
    // indexing an array becomes a subquery with a CAST over it, the type-mapping inference from
    // the other side does not propagate inside, and the parameter is left without a mapping --
    // "in the SQL tree does not have a type mapping assigned",
    // SetOperationsRequireAtLeastOneSideWithValidTypeMapping, or a plain translation failure. This
    // provider does not reach that state, and the base tests' own result assertions hold, so the
    // answers are right rather than merely un-thrown (measured in R31, not inferred).
    // WHY it does not reach that state was measured on 2026-09-15, and it is ADR-006: a compiled
    // query's parameters cross as values, and the server's EF evaluates what is built on them.
    //
    // "Not overridden", this said at R31: there is no grandparent to call, and asserting the correct
    // behaviour to turn the red green would be overriding a spec test to make the suite green, which
    // CLAUDE.md then forbade. V12 overrode them with the core body on the owner's decision, and the
    // rule itself was replaced on 2026-09-15 by an override that says why. This is the R29
    // category (`OwnedJson.Associate_with_parameter_null`) once more: a query this provider answers
    // that other EF providers refuse, which is `website/docs/limitations.md`'s territory. It said
    // "two more times" until 2026-09-22, two days after #122 took the Union test out of it.

    private static async Task AssertApplyNotSupported(Func<Task> query)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(query)).Message);

    public class PrimitiveCollectionsQuerySqliteInfoCarrierFixture : PrimitiveCollectionsQueryFixtureBase, ITestSqlLoggerFactory
    {
        /// <summary>
        ///     The compliance gate's second assertion (R54). The property is real —
        ///     <c>InfoCarrierTestStoreFactory.CreateListLoggerFactory</c> returns a
        ///     <c>TestSqlLoggerFactory</c> — but what it observes is the <em>client's</em> log, and
        ///     this client has no database and emits no SQL. <c>ServerSqlLog</c> is where the
        ///     server's statements can actually be read.
        /// </summary>
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "PrimitiveCollectionsQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
