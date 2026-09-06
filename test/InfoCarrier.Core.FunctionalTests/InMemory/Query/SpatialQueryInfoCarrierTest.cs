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
///         <b>The CORE base, and the relational one was dropped when the test projects split.</b>
///         R50 had adopted <c>SpatialQueryRelationalTestBase</c> here, and reading what that base
///         actually contains is what decided it: fourteen lines, <b>no tests of its own</b>, and a
///         <c>RelationalQueryAsserter</c> whose only difference from the core one is calling
///         <c>TestSqlLoggerFactory.OutputSql()</c> when an assertion fails. On a client with no
///         database there is no SQL to output. So the relational base bought nothing here and cost
///         Tier A a reference to the relational specification assembly, which the split forbids —
///         a relational client over an InMemory backend is the disagreement the seam exists to
///         prevent. The test count is unchanged, because the base declared no tests to lose.
///     </para>
/// </remarks>
public class SpatialQueryInfoCarrierTest(SpatialQueryInfoCarrierTest.InfoCarrierFixture fixture)
    : SpatialQueryTestBase<SpatialQueryInfoCarrierTest.InfoCarrierFixture>(fixture)
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

    public class InfoCarrierFixture : SpatialQueryFixtureBase
    {
        private ITestStoreFactory? _testStoreFactory;

        protected override string StoreName
            => "SpatialQueryInfoCarrierTest";

        // NO `TestSqlLoggerFactory`, and losing it is what the project split bought. It lives in
        // `EFCore.Relational.Specification.Tests`, which Tier A does not reference. It was here for
        // `RelationalComplianceTestBase`'s second assertion (R54), and Tier A is now checked by the
        // plain `ComplianceTestBase`, which does not ask. What it returned was the CLIENT's log
        // anyway, and this client has no database and emits no SQL.

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
