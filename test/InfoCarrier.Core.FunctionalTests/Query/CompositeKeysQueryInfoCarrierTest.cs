// Licensed under the MIT license. See license.txt file in the project root for license information.

using InfoCarrier.Core.FunctionalTests.TestUtilities;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace InfoCarrier.Core.FunctionalTests.Query;

/// <summary>
///     <c>CompositeKeysQueryTestBase</c> on Tier A.
/// </summary>
/// <remarks>
///     A whole query base over a model keyed on two properties. `CompositeKeyEndToEnd` covers the
///     tracking side and is green; this is the query side, which nothing has exercised — every
///     identity path here builds a key array and a query that projects, joins or includes across
///     one is where an off-by-one in that array shows.
/// </remarks>
public class CompositeKeysQueryInfoCarrierTest(CompositeKeysQueryInfoCarrierFixture fixture)
    : CompositeKeysQueryTestBase<CompositeKeysQueryInfoCarrierFixture>(fixture);

/// <summary>
///     The composite-keys query fixture, wired to an InMemory backend behind the wire.
/// </summary>
public class CompositeKeysQueryInfoCarrierFixture : CompositeKeysQueryFixtureBase
{
    private ITestStoreFactory? _testStoreFactory;

    /// <inheritdoc />
    protected override ITestStoreFactory TestStoreFactory
        => _testStoreFactory ??= InfoCarrierTestStoreFactory.Create(
            InfoCarrierTestStoreFactory.InMemory,
            ContextType,
            (modelBuilder, context) => OnModelCreating(modelBuilder, context),
                configureConventions: ConfigureConventions);
}
