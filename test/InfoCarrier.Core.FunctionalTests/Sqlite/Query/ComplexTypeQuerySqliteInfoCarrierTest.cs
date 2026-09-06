// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Sqlite.Internal;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

// Internal EF Core API usage. This provider is built on EF Core internals by design
// (CLAUDE.md), and EF Core's own providers suppress EF1001 the same way at the point of use.
#pragma warning disable EF1001

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>ComplexTypeQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     Querying <em>against</em> complex types — filtering on a member of one, projecting one,
///     comparing two — as opposed to <c>ComplexTypesTrackingTestBase</c>, which tracks them and
///     runs happily on Tier A. A77 established why the two differ: EF's InMemory provider does not
///     translate a complex property access at all, and ships no complex-type query test of any
///     kind. The SQLite one does, so this is the tier the base belongs on.
///     <para>
///         <b>R32 re-parented this onto <c>ComplexTypeQueryRelationalTestBase</c>, and the remark
///         that stood here was wrong.</b> It said the relational base "asserts SQL, which a client
///         with no database has none of". It does not: its six overrides each assert an
///         <em>exception message</em> and then call an empty <c>AssertSql()</c> meaning "nothing
///         was executed". That is the same C0-era misreading R30 corrected for the
///         <c>ComplexJson</c> bases, and it had cost this file six hand-written copies of
///         overrides it could have inherited.
///     </para>
///     <para>
///         What the empty <c>AssertSql()</c> is worth here, stated plainly: it reads the
///         <em>client's</em> <c>TestSqlLoggerFactory</c>, and this client emits no SQL, so it
///         passes trivially. Weaker than on SQLite, not false.
///     </para>
/// </remarks>
public class ComplexTypeQuerySqliteInfoCarrierTest(
    ComplexTypeQuerySqliteInfoCarrierTest.ComplexTypeQuerySqliteInfoCarrierFixture fixture)
    : ComplexTypeQueryRelationalTestBase<ComplexTypeQuerySqliteInfoCarrierTest.ComplexTypeQuerySqliteInfoCarrierFixture>(
        fixture)
{
    // R32 deleted six overrides here -- the two `Subquery_over_*` and the four
    // `Concat_`/`Union_two_different_*`. Every one was `ComplexTypeQueryRelationalTestBase`'s,
    // copied in by hand because that base was believed to assert SQL, and the re-parent now
    // inherits them verbatim. What is left below is EF's `ComplexTypeQuerySqliteTest` pair, which
    // the relational base does not carry.

    /// <summary>
    ///     <c>ComplexTypeQuerySqliteTest</c>'s two: the query reaches SQL and asks SQLite for
    ///     <c>APPLY</c>, which it does not have.
    /// </summary>
    public override async Task Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async)))
            .Message);

    /// <inheritdoc cref="Same_entity_with_complex_type_projected_twice_with_pushdown_as_part_of_another_projection" />
    public override async Task Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(bool async)
        => Assert.Equal(
            SqliteStrings.ApplyNotSupported,
            (await Assert.ThrowsAsync<InvalidOperationException>(
                () => base.Same_complex_type_projected_twice_with_pushdown_as_part_of_another_projection(async)))
            .Message);

    /// <remarks>
    ///     Moved onto <c>ComplexTypeQueryRelationalFixtureBase</c> in R32, which the relational
    ///     base's <c>TFixture</c> constraint requires. That base adds one member and no model: the
    ///     <c>ITestSqlLoggerFactory</c> implementation, which also drops this fixture from the
    ///     compliance test's second list.
    /// </remarks>
    public class ComplexTypeQuerySqliteInfoCarrierFixture : ComplexTypeQueryRelationalFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "ComplexTypeQuerySqliteInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                SqliteInfoCarrierTier.Instance,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
