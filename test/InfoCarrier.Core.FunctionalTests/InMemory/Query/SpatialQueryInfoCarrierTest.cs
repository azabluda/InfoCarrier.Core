// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>Query.SpatialQueryRelationalTestBase</c> on ADR-009 Tier A. See
///     <see cref="SpatialInfoCarrierTest" /> for why Tier A and what had to exist first.
/// </summary>
/// <remarks>
///     <para>
///         EF's <c>SpatialQueryInMemoryTest</c> carries four overrides. Each was checked against
///         ours by reason before being borrowed (A63) — see the comment on each.
///     </para>
///     <para>
///         <b>The relational base, since R50 — and it needs no SpatiaLite and no store change.</b>
///         <c>SpatialQueryRelationalTestBase</c> is fourteen lines and adds no tests: its whole
///         contribution is a <c>RelationalQueryAsserter</c>, which differs from the core one by
///         calling <c>TestSqlLoggerFactory.OutputSql()</c> when an assertion fails. So the cost
///         is the <see cref="ITestSqlLoggerFactory" /> member below, the count is unchanged at
///         <c>Passed: 168, Failed: 0, Total: 168</c>, and this base was priced as needing a
///         native library on its name rather than its body.
///     </para>
/// </remarks>
public class SpatialQueryInfoCarrierTest(SpatialQueryInfoCarrierTest.InfoCarrierFixture fixture)
    : SpatialQueryRelationalTestBase<SpatialQueryInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    /// <remarks>
    ///     Ours raises the same <see cref="NullReferenceException" /> from the same place: the
    ///     stack ends in <c>NetTopologySuite.Geometries.Geometry.Intersects</c>, inside the
    ///     InMemory backend's own compiled projection lambda. The store's, not the wire's.
    /// </remarks>
    public override Task Intersects_equal_to_null(bool async)
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Intersects_equal_to_null(async));

    /// <inheritdoc />
    /// <remarks>As <see cref="Intersects_equal_to_null" />.</remarks>
    public override Task Intersects_not_equal_to_null(bool async)
        => Assert.ThrowsAsync<NullReferenceException>(() => base.Intersects_not_equal_to_null(async));

    /// <inheritdoc />
    /// <remarks>
    ///     EF's override is a bare no-op with no stated reason; ours is measured. The failure is
    ///     <c>ApplicationException: null geometries are not supported</c>, raised by
    ///     NetTopologySuite inside the InMemory backend's compiled lambda — server-side, before
    ///     anything reaches the wire. A Tier A store limitation.
    /// </remarks>
    public override Task Distance_constant_lhs(bool async)
        => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>
    ///     EF's comment on its own override is <c>// Sequence contains no elements</c>, and ours
    ///     fails with exactly <c>InvalidOperationException : Sequence contains no elements</c>.
    /// </remarks>
    public override Task GetGeometryN_with_null_argument(bool async)
        => Task.CompletedTask;

    public class InfoCarrierFixture : SpatialQueryFixtureBase, ITestSqlLoggerFactory
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "SpatialQueryInfoCarrierTest";

        /// <inheritdoc />
        public TestSqlLoggerFactory TestSqlLoggerFactory
            => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
