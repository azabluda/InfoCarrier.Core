// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory;

/// <summary>
///     <c>SpatialTestBase</c> on ADR-009 Tier A.
/// </summary>
/// <remarks>
///     <para>
///         Tier A, not Tier B, which is the opposite of where instinct puts a spatial suite (C0):
///         EF ships both an InMemory and a SQLite one, SQLite's needs the SpatiaLite native
///         library and InMemory's needs only the managed NetTopologySuite types. Under A81's
///         "exactly one tier" rule the cheaper green is the one to take, and v1 shipped these two
///         classes on its InMemory tier as well (C12).
///     </para>
///     <para>
///         Two things had to exist first, and C9 discovered them the hard way by attempting both
///         at once: C15's type-mapping branch, without which the client model refuses to map a
///         geometry at all, and C17's value-mapper seam, without which a geometry that *does*
///         travel meets <c>Geometry.Boundary</c> and <c>Geometry.Envelope</c> in the reflective
///         object walk and recurses until the host aborts.
///     </para>
/// </remarks>
public class SpatialInfoCarrierTest(SpatialInfoCarrierTest.InfoCarrierFixture fixture)
    : SpatialTestBase<SpatialInfoCarrierTest.InfoCarrierFixture>(fixture)
{
    /// <inheritdoc />
    protected override void UseTransaction(DatabaseFacade facade, IDbContextTransaction transaction)
        => facade.UseInfoCarrierTransaction(transaction);

    public class InfoCarrierFixture : SpatialFixtureBase
    {
        private ITestStoreFactory _testStoreFactory;

        protected override string StoreName
            => "SpatialInfoCarrierTest";

        protected override ITestStoreFactory TestStoreFactory
            => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
                InfoCarrierTestStoreFactory.InMemory,
                ContextType,
                (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
    }
}
