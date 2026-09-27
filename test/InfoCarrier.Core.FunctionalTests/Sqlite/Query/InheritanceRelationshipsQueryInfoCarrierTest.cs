// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Sqlite.Query;

/// <summary>
///     <c>InheritanceRelationshipsQueryTestBase</c> on ADR-009 <b>Tier B</b>.
/// </summary>
/// <remarks>
///     <para>
///         A17 adopted the hierarchy queried through its base set; this is the harder half —
///         navigations that *cross* a hierarchy, where the entity type on either end of a
///         relationship is a derived one and the wire has to name it exactly. EF's own InMemory and
///         SQLite tests need no failing override, so anything red here is ours.
///     </para>
///     <para>
///         <b>Moved from Tier A on 2026-09-27, at the owner's request</b>, so that #167's slow run
///         can compare it with plain EF; Tier A's store runs no statement. Base and fixture follow
///         EF's own SQLite class.
///     </para>
/// </remarks>
public class InheritanceRelationshipsQueryInfoCarrierTest(InheritanceRelationshipsQueryInfoCarrierFixture fixture)
    : InheritanceRelationshipsQueryRelationalTestBase<InheritanceRelationshipsQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The inheritance-relationships fixture, wired to a SQLite backend behind the wire.
/// </summary>
public class InheritanceRelationshipsQueryInfoCarrierFixture : InheritanceRelationshipsQueryRelationalFixture
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            SqliteInfoCarrierTier.Instance,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
            configureConventions: ConfigureConventions);
}
