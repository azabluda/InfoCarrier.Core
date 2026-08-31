// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.Sqlite.Query.Associations;
using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>OwnedQueryRelationalTestBase</c> on ADR-009 <b>Tier B</b>, mirroring EF's own
///     <c>OwnedQuerySqliteTest</c>, which is nineteen lines and overrides nothing.
/// </summary>
/// <remarks>
///     <para>
///         An owned entity type has no identity of its own: it is addressed through its owner, and
///         the wire has to carry it that way (ADR-008).
///     </para>
///     <para>
///         <b>This is a tier move, not a second class.</b> <c>OwnedQueryTestBase</c> ran on Tier A
///         until R69, where its five overrides were all InMemory limitations copied from EF's own
///         <c>OwnedQueryInMemoryTest</c>. A base belongs to exactly one tier, and when it could go
///         either way the tier that <em>translates</em> is the one whose green means more — so the
///         Tier A class is gone rather than duplicated. Re-parenting onto the relational base adds
///         the eight <c>*_split</c> theories and the <c>FromSql</c> one, and deletes all five
///         overrides: on a store that composes, every one of those queries simply answers.
///     </para>
///     <para>
///         <b>The row-limiting warning is forwarded to the server, and that is C69's mechanism
///         rather than a new one.</b> The relational base overrides
///         <c>ElementAt_over_owned_collection</c>, <c>ElementAtOrDefault_over_owned_collection</c>
///         and <c>Skip_Take_over_owned_collection</c> to expect an
///         <see cref="InvalidOperationException" />, naming the reason in a comment on each: a row
///         limiting operator without an <c>OrderBy</c>. That diagnostic is
///         <c>RowLimitingOperationWithoutOrderByWarning</c>, raised by
///         <c>RelationalQueryableMethodTranslatingExpressionVisitor</c> — the <em>backing store's</em>
///         translator — and the server does not inherit the fixture's <c>ConfigureWarnings</c>, so
///         without <see cref="AssociationsWarnings.ThrowOnUnorderedRowLimiting" /> the query runs
///         and returns whatever the store's natural order gives.
///     </para>
///     <para>
///         <b>Why not forward the fixture's whole warning configuration.</b> That was measured, in
///         C55, at <b>8 fixed and 626 broken</b>: most of the 626 are model warnings about a model
///         <c>TestModelSource</c> built for the backing store rather than one the caller wrote.
///         The single event is forwarded instead, per fixture, exactly as the four
///         <c>Associations</c> fixtures do — and for the same reason, that the base names this
///         event itself.
///     </para>
///     <para>
///         <b>What stays red: the two <c>FromSql</c> parameterizations</b> of
///         <c>Using_from_sql_on_owner_generates_join_with_table_for_owned_shared_dependents</c>,
///         which is #60. They die on <c>RelationalOwnedQueryFixture</c>'s
///         <c>public new RelationalTestStore TestStore</c> cast, reached through
///         <c>NormalizeDelimitersInRawString</c>. <b>That cast is on no other route in this base</b>,
///         so ADR-013's 2026-08-30 amendment says the base still adopts.
///     </para>
/// </remarks>
public class OwnedQueryInfoCarrierTest(OwnedQueryInfoCarrierTest.OwnedQueryInfoCarrierFixture fixture)
    : OwnedQueryRelationalTestBase<OwnedQueryInfoCarrierTest.OwnedQueryInfoCarrierFixture>(fixture)
{
    /// <summary>
    ///     The owned-query fixture, wired to a SQLite backend behind the wire. Nested, because
    ///     <c>RelationalOwnedQueryFixture</c> is nested in the base it belongs to — EF's own SQLite
    ///     class nests its fixture for the same reason.
    /// </summary>
    /// <remarks>
    ///     No <c>TestSqlLoggerFactory</c> property is declared here: unlike the Tier A fixture this
    ///     replaces, <c>RelationalOwnedQueryFixture</c> already implements
    ///     <c>ITestSqlLoggerFactory</c> and supplies it. That is the R1 payoff again — the
    ///     re-parent deletes a duplicate rather than adding one.
    /// </remarks>
    public class OwnedQueryInfoCarrierFixture : RelationalOwnedQueryFixture
    {
        private ITestStoreFactory? _testStoreFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.Sqlite,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                onAddOptions: AssociationsWarnings.ThrowOnUnorderedRowLimiting,
                configureConventions: ConfigureConventions);
    }
}
