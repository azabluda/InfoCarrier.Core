// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.InMemory.Query;

/// <summary>
///     <c>ManyToManyQueryTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     The query side of the model A5–A13 spent nine steps on from the loading side. Join rows,
///     shared-type join entities and unidirectional skip navigations all reach the wire here
///     through <c>Include</c> and projection rather than through an explicit load.
/// </remarks>
public class ManyToManyQueryInfoCarrierTest(ManyToManyQueryInfoCarrierFixture fixture)
    : ManyToManyQueryTestBase<ManyToManyQueryInfoCarrierFixture>(fixture);

/// <summary>
///     <c>ManyToManyNoTrackingQueryTestBase</c> on Tier A — the same queries with nothing tracked,
///     which is the path that has to answer "loaded" from the entity rather than from an entry.
/// </summary>
public class ManyToManyNoTrackingQueryInfoCarrierTest(ManyToManyQueryInfoCarrierFixture fixture)
    : ManyToManyNoTrackingQueryTestBase<ManyToManyQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The many-to-many query fixture, wired to an InMemory backend behind the wire.
/// </summary>
public class ManyToManyQueryInfoCarrierFixture : ManyToManyQueryFixtureBase
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
