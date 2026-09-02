// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>InheritanceRelationshipsQueryTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A17 adopted the hierarchy queried through its base set; this is the harder half — navigations
///     that *cross* a hierarchy, where the entity type on either end of a relationship is a derived
///     one and the wire has to name it exactly. EF's own InMemory test needs no overrides at all,
///     so anything red here is ours.
/// </remarks>
public class InheritanceRelationshipsQueryInfoCarrierTest(InheritanceRelationshipsQueryInfoCarrierFixture fixture)
    : InheritanceRelationshipsQueryTestBase<InheritanceRelationshipsQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The inheritance-relationships fixture, wired to an InMemory backend behind the wire.
/// </summary>
public class InheritanceRelationshipsQueryInfoCarrierFixture : InheritanceRelationshipsQueryFixtureBase
{
    // NO `TestSqlLoggerFactory` AND NO `ITestSqlLoggerFactory`, and losing them is what the
    // project split bought. Both live in `EFCore.Relational.Specification.Tests`, which Tier A
    // does not reference. They were here for `RelationalComplianceTestBase`'s second assertion
    // (R54), and Tier A is now checked by the plain `ComplianceTestBase`, which does not ask.
    // What the property returned was the CLIENT's log anyway, and this client has no database and
    // emits no SQL; `ServerSqlLog` is where the server's statements can be read.

    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
}
